using System;
using System.Net.Http;
using AvaloniaChatClient.Adapters;
using AvaloniaChatClient.Adapters.OpenAi;
using AvaloniaChatClient.Backend.Models;

namespace AvaloniaChatClient.Backend.Services;

public class AdapterFactory
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AdapterFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public ILlmAdapter Create(ServerProfile profile)
    {
        var http = _httpClientFactory.CreateClient();

        return profile.Protocol switch
        {
            LlmProtocol.OpenAI => new OpenAiAdapter(http),
            LlmProtocol.LmStudio => new OpenAiAdapter(http),   // LM Studio uses OpenAI-compat API
            LlmProtocol.Anthropic => throw new NotSupportedException("Anthropic adapter not yet implemented."),
            LlmProtocol.Custom => throw new NotSupportedException("Custom adapter not yet implemented."),
            _ => throw new ArgumentOutOfRangeException(nameof(profile.Protocol))
        };
    }
}
