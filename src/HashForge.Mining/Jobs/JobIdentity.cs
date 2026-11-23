namespace HashForge.Mining.Jobs;

/// <summary>A job's unique id and the sequence number of the template it is built from.</summary>
public sealed record JobIdentity(string Id, long TemplateSequence);
