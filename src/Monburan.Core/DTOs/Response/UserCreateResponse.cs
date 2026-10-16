using System.Text.Json.Serialization;

namespace Monburan.Core.DTOs.Response
{
    public class UserCreateResponse
    {
        public UserCreateResponse()
        {
            Errors = [];
        }

        public UserCreateResponse(bool sucesso = true) : this()
        {
            Success = sucesso;
        }

        [JsonInclude]
        public List<string> Errors { get; private set; }

        [JsonInclude]
        public bool Success { get; private set; }

        public void AddErros(IEnumerable<string> errors)
        {
            Errors.AddRange(errors);
        }
    }
}