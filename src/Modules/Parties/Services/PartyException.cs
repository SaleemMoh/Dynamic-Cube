namespace DynamicCube.Modules.Parties.Services;

public sealed class PartyNotFoundException(string message) : Exception(message);

/// <summary>The operation conflicts with the party's current state.</summary>
public sealed class PartyConflictException(string message) : Exception(message);
