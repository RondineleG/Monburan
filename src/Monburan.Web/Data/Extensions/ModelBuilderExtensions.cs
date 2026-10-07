using Microsoft.EntityFrameworkCore;

using Monburan.Core.Entities.Base;
using Monburan.Web.Data.Configurations;

using System.Reflection;

namespace Monburan.Web.Data.Extensions;

public static partial class ModelBuilderExtensions
{
    private static readonly Lazy<Type[]> _configurationTypes = new(
        () =>
        Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(
                t => t.IsClass &&
                        !t.IsAbstract &&
                        t.GetInterfaces()
                            .Any(
                                i => i.IsGenericType &&
                                                (i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))))
            .ToArray());

    public static void ApplyEntityConfigurations(this ModelBuilder builder)
    {
        var applyConfigurationMethod = typeof(ModelBuilder)
            .GetMethods()
            .First(
                m =>
                (m.Name == nameof(ModelBuilder.ApplyConfiguration)) &&
                    (m.GetParameters().Length == 1));

        foreach (var configurationType in _configurationTypes.Value)
        {
            if (configurationType.IsGenericTypeDefinition ||
                (configurationType == typeof(BaseEntityConfiguration<>)))
            {
                continue;
            }

            var interfaceType = configurationType
                .GetInterfaces()
                .First(
                    i => i.IsGenericType &&
                        (i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)));

            var entityType = interfaceType.GetGenericArguments()[0];

            var instance = Activator.CreateInstance(configurationType)!;
            var genericMethod = applyConfigurationMethod.MakeGenericMethod(entityType);

            genericMethod.Invoke(builder, new[] { instance });
        }

        ApplyBaseEntityConfigurations(builder);
    }

    private static void ApplyBaseEntityConfigurations(ModelBuilder builder)
    {
        var applyConfigurationMethod = typeof(ModelBuilder)
            .GetMethods()
            .First(
                m =>
                (m.Name == nameof(ModelBuilder.ApplyConfiguration)) &&
                    (m.GetParameters().Length == 1));

        var baseEntityType = typeof(BaseEntity);

        var entityTypes = builder.Model
            .GetEntityTypes()
            .Where(et => baseEntityType.IsAssignableFrom(et.ClrType))
            .Select(et => et.ClrType)
            .ToList();

        foreach (var entityType in entityTypes)
        {
            var configurationType = typeof(BaseEntityConfiguration<>).MakeGenericType(entityType);
            var configurationInstance = Activator.CreateInstance(configurationType)!;

            var genericMethod = applyConfigurationMethod.MakeGenericMethod(entityType);
            genericMethod.Invoke(builder, new[] { configurationInstance });
        }
    }
}