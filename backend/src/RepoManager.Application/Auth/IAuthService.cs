namespace RepoManager.Application.Auth;

public interface IAuthService
{
    Task<TokenResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default);
    Task<TokenResponseDto> RefreshAsync(RefreshTokenDto dto, CancellationToken ct = default);
    Task<UserDto> SetupAsync(SetupDto dto, CancellationToken ct = default);
    Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default);
    Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserDto dto, CancellationToken ct = default);
    Task DeleteUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<UserDto>> ListUsersAsync(CancellationToken ct = default);
    Task<bool> AdminExistsAsync(CancellationToken ct = default);
}
