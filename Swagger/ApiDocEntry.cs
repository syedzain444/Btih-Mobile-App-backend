namespace HospitalMobileAPPApi.Swagger
{
    internal sealed class ApiDocEntry
    {
        public ApiDocEntry(
            string summary,
            string description,
            string? requestExample = null,
            string? responseExample = null,
            IReadOnlyDictionary<string, string>? parameterDescriptions = null)
        {
            Summary = summary;
            Description = description;
            RequestExample = requestExample;
            ResponseExample = responseExample;
            ParameterDescriptions = parameterDescriptions ?? new Dictionary<string, string>();
        }

        public string Summary { get; }
        public string Description { get; }
        public string? RequestExample { get; }
        public string? ResponseExample { get; }
        public IReadOnlyDictionary<string, string> ParameterDescriptions { get; }
    }
}
