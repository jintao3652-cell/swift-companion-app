using System;

namespace SwiftBridge.Models
{
    public class MessageDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } // "private", "radio", "system"
        public string MessageType { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Content { get; set; }
        public int? Frequency { get; set; }
        public bool MentionedUs { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class AircraftStateDto
    {
        public bool Success { get; set; }
        public string Callsign { get; set; }
        public bool Connected { get; set; }
        public PositionDto Position { get; set; }
        public int Heading { get; set; }
        public int GroundSpeed { get; set; }
        public string Squawk { get; set; }
        public string Status { get; set; }  // depTaxi, enRoute, etc.
        public int Com1Frequency { get; set; }
        public int Com2Frequency { get; set; }
        public bool OnGround { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class PositionDto
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Altitude { get; set; }
    }

    public class SendMessageRequest
    {
        public string MessageType { get; set; } // "private" or "radio"
        public string Recipient { get; set; } // For private messages
        public string Message { get; set; }
    }

    public class OperationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }

        public static OperationResult Ok() => new OperationResult { Success = true };
        public static OperationResult Failure(string message) => new OperationResult { Success = false, Message = message };
    }

    public class ConnectionStatusDto
    {
        public bool Connected { get; set; }
        public string Callsign { get; set; }
        public string Server { get; set; }
        public DateTime? ConnectedAt { get; set; }
    }

    public class PushNotificationDto
    {
        public string Title { get; set; }
        public string Body { get; set; }
        public Dictionary<string, string> Data { get; set; }
    }

    public class PluginEventDto
    {
        public string Type { get; set; }
        public System.Text.Json.JsonElement Payload { get; set; }
    }

    public class AtcListDto
    {
        public string Callsign { get; set; }
        public int Frequency { get; set; }
        public string Name { get; set; }
        public string FacilityType { get; set; }
    }

    public class NearbyAircraftDto
    {
        public string Callsign { get; set; }
        public PositionDto Position { get; set; }
        public int Heading { get; set; }
        public int GroundSpeed { get; set; }
        public double DistanceNm { get; set; }
    }

    public class ControllerInfoDto
    {
        public string Callsign { get; set; }
        public string Frequency { get; set; } // datafeed 已是正确字符串 "128.325"
        public string Type { get; set; }      // CENTER/APP-DEP/TOWER/GROUND/CLR/RAMP/ATIS
        public string Atis { get; set; }
    }
}
