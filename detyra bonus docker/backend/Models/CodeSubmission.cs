namespace CodeLabAPI.Models
{
    public class CodeSubmission
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public required string Language { get; set; } // "python", "csharp"
        public required string Code { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
        public User? User { get; set; }
    }
}
