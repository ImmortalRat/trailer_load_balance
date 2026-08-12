using System.Text.Json;
using System.Text.Json.Serialization;
using TrailerLoadBalance.Web.Models;

namespace TrailerLoadBalance.Web.Services;

/// <summary>
/// Loads trailer profiles bundled as static JSON files under Data/TrailerProfiles at startup.
/// Adding/updating a trailer profile requires editing/adding a JSON file here and redeploying -
/// there is no in-app profile designer, by design.
/// </summary>
public sealed class TrailerProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IReadOnlyList<TrailerProfile> _profiles;

    public TrailerProfileService(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "Data", "TrailerProfiles");
        var profiles = new List<TrailerProfile>();

        if (Directory.Exists(dir))
        {
            foreach (var file in Directory.EnumerateFiles(dir, "*.json").OrderBy(f => f))
            {
                var json = File.ReadAllText(file);
                var profile = JsonSerializer.Deserialize<TrailerProfile>(json, JsonOptions)
                    ?? throw new InvalidOperationException($"Failed to parse trailer profile: {file}");
                profiles.Add(profile);
            }
        }

        _profiles = profiles;
    }

    public IReadOnlyList<TrailerProfile> GetAll() => _profiles;

    public TrailerProfile? GetById(string id) => _profiles.FirstOrDefault(p => p.Id == id);
}
