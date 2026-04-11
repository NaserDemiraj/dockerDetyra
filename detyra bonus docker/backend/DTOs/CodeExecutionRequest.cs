namespace CodeLabAPI.DTOs
{
    public class CodeExecutionRequest
    {
        public required string Language { get; set; } // "python" or "csharp"
        public required string Code { get; set; }
    }

    public class CodeExecutionResponse
    {
        public bool Success { get; set; }
        public string Output { get; set; } = string.Empty;
        public string? Error { get; set; }
        public int ExecutionTimeMs { get; set; }
    }
}
