using Monburan.Core.Enums;

namespace Monburan.Core.DTOs.Request;

/// <summary>Corpo da mudança de situação da vaga pela API; dono e vaga vêm da rota/autenticação.</summary>
public sealed record UpdateJobStatusRequest(EJobStatus NewStatus);
