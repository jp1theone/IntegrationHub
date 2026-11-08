namespace IntegrationHub.Application.Common;

public sealed class BusinessConflictException(string message) : Exception(message);
