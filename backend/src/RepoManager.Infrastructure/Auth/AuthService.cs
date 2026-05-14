using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using RepoManager.Application.Auth;
using RepoManager.Application.Common.Exceptions;
using RepoManager.Domain.Entities;
using RepoManager.Domain.Enums;
using RepoManager.Infrastructure.Persistence;

namespace RepoManager.Infrastructure.Auth;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, IConfiguration config, ILogger<AuthService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<TokenResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email && u.IsActive, ct)
            ?? throw new NotFoundException("User", dto.Email);

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new ConflictException("Invalid credentials.");

        var (accessToken, refreshToken) = GenerateTokens(user);
        user.RefreshTokenHash = HashToken(refreshToken);
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("User {UserId} logged in", user.Id);
        return new TokenResponseDto(accessToken, refreshToken);
    }

    public async Task<TokenResponseDto> RefreshAsync(RefreshTokenDto dto, CancellationToken ct = default)
    {
        var hash = HashToken(dto.RefreshToken);
        var user = await _db.Users.FirstOrDefaultAsync(
            u => u.RefreshTokenHash == hash && u.IsActive, ct)
            ?? throw new ConflictException("Invalid or expired refresh token.");

        if (user.RefreshTokenExpiresAt < DateTimeOffset.UtcNow)
            throw new ConflictException("Refresh token has expired.");

        var (accessToken, newRefreshToken) = GenerateTokens(user);
        user.RefreshTokenHash = HashToken(newRefreshToken);
        user.RefreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(30);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);
        return new TokenResponseDto(accessToken, newRefreshToken);
    }

    public async Task<UserDto> SetupAsync(SetupDto dto, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Role == Role.Admin, ct))
            throw new ConflictException("Setup already completed.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 12),
            Role = Role.Admin,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Initial Admin account created for {Email}", dto.Email);
        return ToDto(user);
    }

    public async Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        if (await _db.Users.AnyAsync(u => u.Email == dto.Email, ct))
            throw new ConflictException($"User with email '{dto.Email}' already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 12),
            Role = dto.Role,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {NewUserId} created with role {Role}", user.Id, user.Role);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("User", userId);

        if (dto.Role.HasValue) user.Role = dto.Role.Value;
        if (dto.IsActive.HasValue) user.IsActive = dto.IsActive.Value;
        if (dto.Password is not null)
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, 12);

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {TargetUserId} updated", userId);
        return ToDto(user);
    }

    public async Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync([userId], ct)
            ?? throw new NotFoundException("User", userId);
        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("User {TargetUserId} deleted", userId);
    }

    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct = default) =>
        await _db.Users.Select(u => ToDto(u)).ToListAsync(ct);

    public async Task<bool> AdminExistsAsync(CancellationToken ct = default) =>
        await _db.Users.AnyAsync(u => u.Role == Role.Admin, ct);

    private (string accessToken, string refreshToken) GenerateTokens(User user)
    {
        var secret = _config["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return (accessToken, refreshToken);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static UserDto ToDto(User u) =>
        new(u.Id, u.Email, u.Role, u.IsActive, u.CreatedAt, u.LastLoginAt);
}
