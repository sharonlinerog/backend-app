namespace CreditosApi.Dtos;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiraEn { get; set; }
}
