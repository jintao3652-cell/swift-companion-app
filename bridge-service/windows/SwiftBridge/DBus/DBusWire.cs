using System.Buffers.Binary;
using System.Text;

namespace SwiftBridge.DBus;

/// <summary>
/// D-Bus 线协议（wire protocol）的最小实现：
/// - 签名解析与通用解码（任意签名 → 树形 DValue）
/// - 消息帧（header + body）的读写
/// 仅覆盖本项目需要的类型：y/b/n/q/i/u/x/t/d/s/o/g/a()v。
/// 字节序固定小端 'l'。签名由调用方显式给出，写入器只负责对齐与编码。
/// </summary>
public static class DBusWire
{
    public const byte MsgMethodCall = 1;
    public const byte MsgMethodReturn = 2;
    public const byte MsgError = 3;
    public const byte MsgSignal = 4;

    // ---------- 值树 ----------

    public sealed class DValue
    {
        public char Kind;              // 'y','b','n','q','i','u','x','t','d','s','o','a','(','v'
        public object? Scalar;         // 基本类型的值
        public List<DValue>? Array;    // 'a'
        public List<DValue>? Struct;   // '('
        public string? Sig;            // 'a' 元素签名 / 'v' variant 签名

        public bool Bool() => Scalar is uint u && u != 0;
        public int Int() => Scalar is int i ? i : 0;
        public long Long() => Scalar is long l ? l : 0;
        public double Double() => Scalar is double d ? d : 0;
        public string Str() => Scalar as string ?? "";

        public override string ToString() => Kind switch
        {
            'a' => $"[{string.Join(",", Array!.Select(v => v.ToString()))}]",
            '(' => $"({string.Join(",", Struct!.Select(v => v.ToString()))})",
            's' or 'o' => $"\"{Scalar}\"",
            _ => Scalar?.ToString() ?? "?"
        };
    }

    /// <summary>把签名里的顶层完整类型切出来（如 "sss(i)x" → ["s","s","s","(i)","x"]）。</summary>
    public static List<string> SplitSignature(string sig)
    {
        var result = new List<string>();
        int i = 0;
        while (i < sig.Length)
        {
            int start = i;
            i = SkipCompleteType(sig, i);
            result.Add(sig[start..i]);
        }
        return result;
    }

    private static int SkipCompleteType(string sig, int i)
    {
        switch (sig[i])
        {
            case 'a':
                return SkipCompleteType(sig, i + 1); // 'a' + 元素类型
            case '(':
                int depth = 0;
                do
                {
                    if (sig[i] == '(') depth++;
                    else if (sig[i] == ')') depth--;
                    i++;
                } while (depth > 0 && i < sig.Length);
                return i;
            default:
                return i + 1;
        }
    }

    // ---------- 解码 ----------

    /// <summary>按签名把消息 body 解码成树。sig 为空返回空列表。</summary>
    public static List<DValue> DecodeBody(string sig, byte[] body)
    {
        var reader = new BodyReader(body);
        var values = new List<DValue>();
        foreach (var type in SplitSignature(sig))
            values.Add(ReadValue(type, reader));
        return values;
    }

    private static DValue ReadValue(string type, BodyReader r)
    {
        char k = type[0];
        switch (k)
        {
            case 'y': return new DValue { Kind = 'y', Scalar = r.ReadByte() };
            case 'b': r.Align(4); return new DValue { Kind = 'b', Scalar = r.ReadU32() };
            case 'n': case 'q': r.Align(2); return new DValue { Kind = k, Scalar = r.ReadU16() };
            case 'i': case 'u': r.Align(4); return new DValue { Kind = k, Scalar = r.ReadU32() };
            case 'x': case 't': case 'd': r.Align(8); return new DValue { Kind = k, Scalar = r.ReadU64OrDouble(k) };
            case 's': case 'o': r.Align(4); return new DValue { Kind = k, Scalar = r.ReadString() };
            case 'g': r.Align(1); return new DValue { Kind = 'g', Scalar = r.ReadSignature() };
            case 'a':
                {
                    string elem = type[1..];
                    r.Align(4);
                    uint byteLen = r.ReadU32();
                    if (elem[0] != 'y')
                        r.Align(elem[0] == '(' || elem[0] == 'a' || elem[0] == 'v' ? 8 : AlignOf(elem[0]));
                    long end = r.Pos + byteLen;
                    var list = new List<DValue>();
                    while (r.Pos < end)
                        list.Add(ReadValue(elem, r));
                    r.Pos = (int)end;
                    return new DValue { Kind = 'a', Array = list, Sig = elem };
                }
            case '(':
                {
                    r.Align(8);
                    var fields = new List<DValue>();
                    foreach (var t in SplitSignature(type[1..^1]))
                        fields.Add(ReadValue(t, r));
                    return new DValue { Kind = '(', Struct = fields };
                }
            case 'v':
                {
                    r.Align(1);
                    string vsig = r.ReadSignature();
                    return new DValue { Kind = 'v', Sig = vsig, Scalar = ReadValue(vsig, r) };
                }
            default:
                throw new FormatException($"Unknown signature char '{k}'");
        }
    }

    private static int AlignOf(char c) => c switch
    {
        'y' or 'g' => 1,
        'n' or 'q' => 2,
        'b' or 'i' or 'u' or 's' or 'o' => 4,
        'x' or 't' or 'd' => 8,
        _ => 8
    };

    private sealed class BodyReader
    {
        private readonly byte[] _buf;
        public int Pos;

        public BodyReader(byte[] buf) { _buf = buf; }

        public void Align(int a)
        {
            int pad = (a - (Pos % a)) % a;
            Pos += pad;
        }

        public byte ReadByte() => _buf[Pos++];
        public ushort ReadU16() { var v = BinaryPrimitives.ReadUInt16LittleEndian(_buf.AsSpan(Pos, 2)); Pos += 2; return v; }
        public uint ReadU32() { var v = BinaryPrimitives.ReadUInt32LittleEndian(_buf.AsSpan(Pos, 4)); Pos += 4; return v; }
        public object ReadU64OrDouble(char k)
        {
            if (k == 'd') { var d = BinaryPrimitives.ReadDoubleLittleEndian(_buf.AsSpan(Pos, 8)); Pos += 8; return d; }
            var v = BinaryPrimitives.ReadUInt64LittleEndian(_buf.AsSpan(Pos, 8)); Pos += 8; return v;
        }
        public string ReadString()
        {
            uint len = ReadU32();
            var s = Encoding.UTF8.GetString(_buf, Pos, (int)len);
            Pos += (int)len + 1; // NUL
            return s;
        }
        public string ReadSignature()
        {
            byte len = ReadByte();
            var s = Encoding.UTF8.GetString(_buf, Pos, len);
            Pos += len + 1;
            return s;
        }
    }

    // ---------- 编码（body） ----------

    /// <summary>流式 body 写入器：对齐 + 编码。签名由调用方显式掌握。</summary>
    public sealed class BodyWriter
    {
        private MemoryStream _ms = new();

        public byte[] ToBytes() => _ms.ToArray();

        private void Align(int a)
        {
            while (_ms.Length % a != 0) _ms.WriteByte(0);
        }

        public void WriteByte(byte v) { Align(1); _ms.WriteByte(v); }
        public void WriteBool(bool v) { Align(4); _ms.Write(BitConverter.GetBytes(v ? 1u : 0u)); }
        public void WriteInt32(int v) { Align(4); _ms.Write(BitConverter.GetBytes(v)); }
        public void WriteInt64(long v) { Align(8); _ms.Write(BitConverter.GetBytes(v)); }
        public void WriteDouble(double v) { Align(8); _ms.Write(BitConverter.GetBytes(v)); }
        public void WriteString(string v)
        {
            Align(4);
            var bytes = Encoding.UTF8.GetBytes(v);
            _ms.Write(BitConverter.GetBytes((uint)bytes.Length));
            _ms.Write(bytes);
            _ms.WriteByte(0);
        }

        /// <summary>结构体起点：8 字节对齐（无显式结束标记，写入器按字节流处理）。</summary>
        public void BeginStruct() => Align(8);

        /// <summary>
        /// 写数组：每个元素在独立临时流中编码（元素内对齐相对元素起点，符合 D-Bus 规范），
        /// 追加后补齐到 8 字节边界（数组元素对齐要求）。
        /// </summary>
        public void WriteArray(int count, Action<BodyWriter, int> elemWriter)
        {
            Align(4);
            var saved = _ms;
            for (int i = 0; i < count; i++)
            {
                _ms = new MemoryStream();
                elemWriter(this, i);
                var elemBytes = _ms.ToArray();
                _ms = saved;
                _ms.Write(elemBytes);
                // 元素之间按 8 字节对齐（结构体元素要求）
                while (_ms.Length % 8 != 0) _ms.WriteByte(0);
            }
        }
    }

    // ---------- 消息帧 ----------

    /// <summary>构造 METHOD_CALL 帧（p2p：无 destination）。</summary>
    public static byte[] BuildMethodCall(uint serial, string path, string iface, string member,
                                         string? bodySig, byte[]? body)
    {
        return BuildMessage(MsgMethodCall, 0, serial, path, iface, member, 0, bodySig, body);
    }

    public static byte[] BuildMethodReturn(uint replySerial, string? bodySig, byte[]? body)
    {
        return BuildMessage(MsgMethodReturn, 1, replySerial, null, null, null, replySerial, bodySig, body);
    }

    private static byte[] BuildMessage(byte type, byte flags, uint serial, string? path,
                                       string? iface, string? member, uint replySerial,
                                       string? bodySig, byte[]? body)
    {
        // header fields: array of struct(byte, variant)
        var fields = new MemoryStream();
        byte[] StrBytes(string s) => Encoding.UTF8.GetBytes(s);

        void WriteFieldHeader(byte code, string vsig)
        {
            fields.WriteByte(code);
            fields.WriteByte((byte)vsig.Length);
            fields.Write(Encoding.ASCII.GetBytes(vsig));
            fields.WriteByte(0);
        }

        void WriteStringField(byte code, string value, string sig)
        {
            WriteFieldHeader(code, sig);
            while (fields.Length % 4 != 0) fields.WriteByte(0);
            var b = StrBytes(value);
            fields.Write(BitConverter.GetBytes((uint)b.Length));
            fields.Write(b);
            fields.WriteByte(0);
        }

        if (path != null) WriteStringField(1, path, "o");       // PATH (o)
        if (iface != null) WriteStringField(2, iface, "s");     // INTERFACE (s)
        if (member != null) WriteStringField(3, member, "s");   // MEMBER (s)
        if (type == MsgMethodReturn)                        // REPLY_SERIAL (u)
        {
            WriteFieldHeader(5, "u");
            while (fields.Length % 4 != 0) fields.WriteByte(0);
            fields.Write(BitConverter.GetBytes(replySerial));
        }
        if (!string.IsNullOrEmpty(bodySig))                 // SIGNATURE (g)
        {
            WriteFieldHeader(8, "g");
            fields.Write(Encoding.ASCII.GetBytes(bodySig));
            fields.WriteByte(0);
        }

        var header1 = new MemoryStream();
        header1.WriteByte((byte)'l');
        header1.WriteByte(type);
        header1.WriteByte(flags);
        header1.WriteByte(1); // protocol version
        // body length (u) + serial (u) — 先占位
        header1.Write(BitConverter.GetBytes((uint)(body?.Length ?? 0)));
        header1.Write(BitConverter.GetBytes(serial));
        // header fields array: 长度前缀 + 内容，需 8 字节对齐补齐到 body 起点
        var fieldsBytes = fields.ToArray();
        header1.Write(BitConverter.GetBytes((uint)fieldsBytes.Length));
        header1.Write(fieldsBytes);
        while (header1.Length % 8 != 0) header1.WriteByte(0);

        var outMs = new MemoryStream();
        outMs.Write(header1.ToArray());
        if (body != null && body.Length > 0)
        {
            // body 需按 body 首类型对齐——由调用方保证 body 从 8 对齐缓冲开始编码时已含对齐；
            // 这里补齐到 8（D-Bus 允许 body 内部再次对齐，简单起见整个 body 前 pad 到 8）
            outMs.Write(body);
        }
        return outMs.ToArray();
    }

    // ---------- 接收解析 ----------

    public sealed class IncomingMessage
    {
        public byte Type;
        public byte Flags;
        public uint Serial;
        public uint ReplySerial;
        public string? Path;
        public string? Interface;
        public string? Member;
        public string? ErrorName;
        public string? BodySignature;
        public byte[] Body = Array.Empty<byte>();
        public List<DValue>? Decoded;
    }

    /// <summary>尝试从缓冲解析一条完整消息。返回消耗的字节数；不足返回 null。</summary>
    public static (IncomingMessage? msg, int consumed) TryParseMessage(byte[] buf, int len)
    {
        if (len < 16) return (null, 0);
        if (buf[0] != 'l' && buf[0] != 'B') throw new FormatException("Unsupported endianness");

        byte type = buf[1];
        byte flags = buf[2];
        uint bodyLen = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(4, 4));
        uint serial = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(8, 4));
        uint fieldsLen = BinaryPrimitives.ReadUInt32LittleEndian(buf.AsSpan(12, 4));

        int fieldsStart = 16;
        int bodyStart = fieldsStart + (int)fieldsLen;
        bodyStart = (bodyStart + 7) & ~7; // 8 字节对齐
        int total = bodyStart + (int)bodyLen;
        if (len < total) return (null, 0);

        var msg = new IncomingMessage { Type = type, Flags = flags, Serial = serial };
        // 解析 header fields（array of struct(byte, variant)）
        var fr = new BodyReader(buf[fieldsStart..(fieldsStart + (int)fieldsLen)]);
        uint end = (uint)fieldsLen;
        while (fr.Pos < end)
        {
            fr.Align(8); // struct 8 对齐（相对数组数据起点）
            byte code = fr.ReadByte();
            byte sigLen = fr.ReadByte();
            var vsig = Encoding.ASCII.GetString(buf, fieldsStart + fr.Pos, sigLen);
            fr.Pos += sigLen + 1;
            if (vsig == "o" || vsig == "s")
            {
                fr.Align(4);
                var s = fr.ReadString();
                switch (code)
                {
                    case 1: msg.Path = s; break;
                    case 2: msg.Interface = s; break;
                    case 3: msg.Member = s; break;
                    case 4: msg.ErrorName = s; break;
                }
            }
            else if (vsig == "u")
            {
                fr.Align(4);
                var v = fr.ReadU32();
                if (code == 5) msg.ReplySerial = v;
            }
            else if (vsig == "g")
            {
                var s = fr.ReadSignature();
                if (code == 8) msg.BodySignature = s;
            }
            else
            {
                break; // 未知类型，放弃该字段解析
            }
        }

        if (bodyLen > 0)
        {
            msg.Body = new byte[bodyLen];
            Array.Copy(buf, bodyStart, msg.Body, 0, bodyLen);
            if (!string.IsNullOrEmpty(msg.BodySignature))
                msg.Decoded = DecodeBody(msg.BodySignature, msg.Body);
        }

        return (msg, total);
    }
}
