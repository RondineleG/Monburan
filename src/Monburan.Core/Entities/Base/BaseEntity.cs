using Monburan.Core.ValueObjects;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace Monburan.Core.Entities.Base;

public abstract class BaseEntity : IAggregateRoot
{
    protected BaseEntity()
    {
        Id = Id.Create();
        CreatedAt = DateTime.UtcNow;
        ValidateInitialState();
    }

    protected BaseEntity(Id id, DateTime createdAt, DateTime? updatedAt = null)
    {
        ValidateConstructorParameters(id, createdAt, updatedAt);
        Id = id;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    protected BaseEntity(Guid id, DateTime createdAt, string tenantId)
    {
        Id = Id.Create(id);
        CreatedAt = createdAt;
        TenantId = tenantId;
    }

    protected BaseEntity(
        Guid id,
        DateTime createdAt,
        string tenantId,
        string createdBy = "",
        DateTime? updatedAt = null,
        string updatedBy = "",
        DateTime? deletedAt = null,
        string deletedBy = "",
        bool isDeleted = false)
    {
        Id = Id.Create(id);
        CreatedAt = createdAt;
        TenantId = tenantId;
        CreatedBy = createdBy;
        UpdatedAt = updatedAt;
        UpdatedBy = updatedBy;
        DeletedAt = deletedAt;
        DeletedBy = deletedBy;
        IsDeleted = isDeleted;
    }

    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = string.Empty;

    public DateTime? DeletedAt { get; set; }

    public string DeletedBy { get; set; } = string.Empty;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public Id Id { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [Required]
    public string TenantId { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; protected set; }

    public string UpdatedBy { get; set; } = string.Empty;

    protected virtual string GetCollectionName()
    {
        return GetType().Name;
    }

    protected void InitializeNewEntity()
    {
        Id = Id.Create();
        CreatedAt = DateTime.UtcNow;
        ValidateInitialState();
    }

    protected void Update()
    {
        UpdatedAt = DateTime.UtcNow;
        ValidateEntityState();
    }

    protected virtual void ValidateEntityState()
    {
        var result = Validations.ValidationResult
            .Success()
            .AddErrorIf(() => CreatedAt == default || CreatedAt.Kind != DateTimeKind.Utc,
                "CreatedAt must be a valid UTC date", nameof(CreatedAt));

        if (UpdatedAt.HasValue)
        {
            result
                .AddErrorIf(() => UpdatedAt.Value.Kind != DateTimeKind.Utc,
                    "UpdatedAt must be UTC", nameof(UpdatedAt))
                .AddErrorIf(() => UpdatedAt.Value <= CreatedAt,
                    "UpdatedAt must be greater than CreatedAt", nameof(UpdatedAt));
        }

        result.ThrowIfInvalid();
    }

    private static void ValidateConstructorParameters(Id id, DateTime createdAt, DateTime? updatedAt)
    {
        var result = Validations.ValidationResult
            .Success()
            .AddErrorIf(() => id == null, "Id cannot be null", nameof(Id))
            .AddErrorIf(() => createdAt == default, "CreatedAt is required", nameof(CreatedAt))
            .AddErrorIf(() => createdAt.Kind != DateTimeKind.Utc, "CreatedAt must be UTC", nameof(CreatedAt));

        if (updatedAt.HasValue)
        {
            result
                .AddErrorIf(() => updatedAt.Value.Kind != DateTimeKind.Utc,
                    "UpdatedAt must be UTC", nameof(UpdatedAt))
                .AddErrorIf(() => updatedAt.Value <= createdAt,
                    "UpdatedAt must be greater than CreatedAt", nameof(UpdatedAt));
        }

        result.ThrowIfInvalid();
    }

    private void ValidateInitialState()
    {
        Validations.ValidationResult
            .Success()
            .AddErrorIf(() => CreatedAt == default || CreatedAt.Kind != DateTimeKind.Utc,
                "CreatedAt must be a valid UTC date", nameof(CreatedAt))
            .AddErrorIf(() => Id == null, "Id cannot be null", nameof(Id))
            .ThrowIfInvalid();
    }
}