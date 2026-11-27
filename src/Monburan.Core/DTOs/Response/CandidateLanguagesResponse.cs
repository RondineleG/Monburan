using Monburan.Core.DTOs.Request;

namespace Monburan.Core.DTOs.Response;

public class CandidateLanguagesResponse
{
    public List<CandidateLanguageItem> Languages { get; set; } = [];
}
