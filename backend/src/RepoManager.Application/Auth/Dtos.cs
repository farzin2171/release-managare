using RepoManager.Domain.Enums;

namespace RepoManager.Application.Auth;

public record LoginDto(string Email, string Password);
public record SetupDto(string Email, string Password);
public record TokenResponseDto(string AccessToken, string RefreshToken, string TokenType = "Bearer");
public record RefreshTokenDto(string RefreshToken);
public record CreateUserDto(string Email, string Password, Role Role);
public record UpdateUserDto(Role? Role, bool? IsActive, string? Password);
public record UserDto(Guid Id, string Email, Role Role, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);
