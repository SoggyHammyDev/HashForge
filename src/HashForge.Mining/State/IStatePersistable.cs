namespace HashForge.Mining.State;

/// <summary>A component whose in-memory state should survive a restart.</summary>
public interface IStatePersistable
{
    string StateKey { get; }

    string ExportState();

    void ImportState(string state);
}
