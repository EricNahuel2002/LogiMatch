namespace Application.Dtos.Auth;

public sealed class TokenUserDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
}