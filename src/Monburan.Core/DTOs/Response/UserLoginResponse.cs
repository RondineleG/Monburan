using System.Text.Json.Serialization;

namespace Monburan.Core.DTOs.Response
{
    public class UserLoginResponse
    {
        public UserLoginResponse() => Erros = [];

        public UserLoginResponse(bool sucesso, string accessToken, string refreshToken) : this()
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
        }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string AccessToken { get; private set; } = string.Empty;

        public List<string> Erros { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string RefreshToken { get; private set; } = string.Empty;

        public bool Sucesso => Erros.Count == 0;

        public void AddErro(string erro)
        {
            Erros.Add(erro);
        }

        public void AddErros(IEnumerable<string> erros)
        {
            Erros.AddRange(erros);
        }
    }
}