using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WorkersPlaces;

public static class WorkerPositions
{
    private const int CurrentVersion = 1;
    private static readonly object Sync = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static Dictionary<Guid, WorkerPosition> _positions = new();
    private static string? _filePath;
    private static Action<string>? _warning;
    private static bool _canWrite;

    public static void Initialize(string saveFolderPath, Action<string>? warning = null)
    {
        if (string.IsNullOrWhiteSpace(saveFolderPath))
        {
            throw new ArgumentException("A save folder path is required.", nameof(saveFolderPath));
        }

        lock (Sync)
        {
            ResetCore();
            _warning = warning;

            string canonicalSavePath = CanonicalizeSavePath(saveFolderPath);
            string saveKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonicalSavePath)));
            string directory = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "WorkersPlaces");
            _filePath = Path.Combine(directory, $"{saveKey}.json");
            _canWrite = true;

            if (!File.Exists(_filePath))
            {
                return;
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                StoreFile? store = JsonSerializer.Deserialize<StoreFile>(json);
                if (store is null || store.Version != CurrentVersion || store.Workers is null)
                {
                    throw new InvalidDataException("The file has an unsupported version or missing worker data.");
                }

                _positions = store.Workers
                    .Where(entry => Guid.TryParse(entry.WorkerId, out _) && entry.Position is not null)
                    .GroupBy(entry => Guid.Parse(entry.WorkerId))
                    .ToDictionary(group => group.Key, group => new WorkerPosition(
                        group.Last().Position!.X,
                        group.Last().Position!.Y,
                        group.Last().Position!.Z,
                        group.Last().Position!.Yaw));
            }
            catch (Exception exception) when (exception is IOException or JsonException or InvalidDataException or UnauthorizedAccessException)
            {
                _canWrite = false;
                _warning?.Invoke($"Could not load worker positions from '{_filePath}'. The file was preserved and will not be overwritten: {exception.Message}");
            }
        }
    }

    public static bool TryGet(Guid workerId, out WorkerPosition position)
    {
        lock (Sync)
        {
            return _positions.TryGetValue(workerId, out position);
        }
    }

    public static bool HasAny
    {
        get
        {
            lock (Sync)
            {
                return _positions.Count > 0;
            }
        }
    }

    public static bool Set(Guid workerId, WorkerPosition position)
    {
        lock (Sync)
        {
            EnsureInitialized();
            if (!_canWrite)
            {
                return false;
            }

            bool hadPrevious = _positions.TryGetValue(workerId, out WorkerPosition previous);
            _positions[workerId] = position;
            try
            {
                SaveCore();
            }
            catch
            {
                if (hadPrevious)
                {
                    _positions[workerId] = previous;
                }
                else
                {
                    _positions.Remove(workerId);
                }

                throw;
            }

            return true;
        }
    }

    public static bool Clear(Guid workerId)
    {
        lock (Sync)
        {
            EnsureInitialized();
            if (!_canWrite)
            {
                return false;
            }

            if (_positions.Remove(workerId, out WorkerPosition previous))
            {
                try
                {
                    SaveCore();
                }
                catch
                {
                    _positions[workerId] = previous;
                    throw;
                }
            }

            return true;
        }
    }

    public static void Reset()
    {
        lock (Sync)
        {
            ResetCore();
        }
    }

    private static string CanonicalizeSavePath(string saveFolderPath)
    {
        string fullPath = Path.GetFullPath(saveFolderPath.Trim());
        string root = Path.GetPathRoot(fullPath) ?? string.Empty;
        string canonicalPath = fullPath.Length > root.Length ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) : fullPath;
        return OperatingSystem.IsWindows() ? canonicalPath.ToUpperInvariant() : canonicalPath;
    }

    private static void SaveCore()
    {
        string filePath = _filePath ?? throw new InvalidOperationException("Worker positions have not been initialized.");
        string directory = Path.GetDirectoryName(filePath)!;
        Directory.CreateDirectory(directory);

        var store = new StoreFile
        {
            Version = CurrentVersion,
            Workers = _positions
                .OrderBy(entry => entry.Key)
                .Select(entry => new WorkerEntry
                {
                    WorkerId = entry.Key.ToString("D"),
                    Position = new PositionDto { X = entry.Value.X, Y = entry.Value.Y, Z = entry.Value.Z, Yaw = entry.Value.Yaw }
                })
                .ToList()
        };

        string temporaryPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(store, JsonOptions));
            if (File.Exists(filePath))
            {
                File.Move(temporaryPath, filePath, overwrite: true);
            }
            else
            {
                File.Move(temporaryPath, filePath);
            }
        }
        catch
        {
            try
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
            catch
            {
            }

            throw;
        }
    }

    private static void EnsureInitialized()
    {
        if (_filePath is null)
        {
            throw new InvalidOperationException("Call WorkerPositions.Initialize with the active save folder first.");
        }
    }

    private static void ResetCore()
    {
        _positions = new Dictionary<Guid, WorkerPosition>();
        _filePath = null;
        _warning = null;
        _canWrite = false;
    }

    private sealed class StoreFile
    {
        public int Version { get; set; }
        public List<WorkerEntry>? Workers { get; set; }
    }

    private sealed class WorkerEntry
    {
        public string WorkerId { get; set; } = string.Empty;
        public PositionDto? Position { get; set; }
    }

    private sealed class PositionDto
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float? Yaw { get; set; }
    }
}

public readonly record struct WorkerPosition(float X, float Y, float Z, float? Yaw);
