namespace FCG.Users.Application.DTOs.Users;

public class TokenDto
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }
}
