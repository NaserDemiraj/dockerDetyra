namespace CodeLabAPI.Models
{
    public class ExecutionResult
    {
        public int Id { get; set; }
        public int CodeSubmissionId { get; set; }
        public required string Output { get; set; }
        public string? ErrorMessage { get; set; }
        public bool Success { get; set; }
        public int ExecutionTimeMs { get; set; }
        public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
        public CodeSubmission? CodeSubmission { get; set; }
    }
}
