using System.ComponentModel.DataAnnotations;

namespace PersonalVidIQ.Application.Options;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Apply pending EF Core migrations when the app starts. Fine for a single-user local tool.</summary>
    public bool AutoMigrate { get; set; } = true;
}
