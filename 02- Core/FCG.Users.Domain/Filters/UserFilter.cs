using System.Text.Json.Serialization;

namespace FCG.Users.Domain.Filters;

public class UserFilter : BaseFilter
{
    [JsonPropertyName("userRoleId")]
    public long? UserRoleId { get; set; }
}
