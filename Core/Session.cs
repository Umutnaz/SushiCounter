namespace Core;

public class Session
{
    public int SessionId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public int userId { get; set; }
}