using FCG.Users.Application.DTOs.Users;

namespace FCG.Users.Application.Services.Interfaces;

public interface ITokenService
{
    TokenDto Generate(ReadUserDto user);
}
