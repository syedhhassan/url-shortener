using SolidShortener.Application.Interfaces.Repositories;
using SolidShortener.Application.Interfaces.Services;
using SolidShortener.Application.Users.Commands;
using SolidShortener.Application.Users.DTOs;
using SolidShortener.Application.Users.Queries;
using SolidShortener.Application.Users.Services.Interfaces;
using SolidShortener.Domain.Entities;

namespace SolidShortener.Application.Users.Services.Implementations;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public UserService(IUserRepository userRepository, IPasswordHasher passwordHasher, ITokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<UserDTO> RegisterUserAsync(RegisterUserCommand command)
    {
        var existing = await _userRepository.GetUserByEmailAsync(command.Email);
        if (existing is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new User
        (
            command.Name,
            command.Email,
            _passwordHasher.HashPassword(command.Password)
        );

        await _userRepository.AddAsync(user);

        return UserMapper.ToDto(user);
    }

    public async Task<UserDTO?> GetUserByIdAsync(Guid id)
    {
        var user = await _userRepository.GetUserByIdAsync(id);

        if (user is null) return null;

        return UserMapper.ToDto(user);
    }

    public async Task<UserDTO?> GetUserByEmailAsync(GetUserByEmailQuery query)
    {
        var user = await _userRepository.GetUserByEmailAsync(query.Email);

        if (user is null) return null;

        return UserMapper.ToDto(user);
    }

    public async Task<AuthResultDTO?> AuthenticateAsync(AuthenticateUserQuery query)
    {
        var user = await _userRepository.GetUserByEmailAsync(query.Email);

        if (user is null) return null;

        if (!_passwordHasher.VerifyPassword(query.Password, user.PasswordHash)) return null;

        var (token, expiresAt) = _tokenGenerator.GenerateToken(UserMapper.ToDto(user));

        return new AuthResultDTO
        {
            Token = token,
            ExpiresAt = expiresAt
        };
    }
}
