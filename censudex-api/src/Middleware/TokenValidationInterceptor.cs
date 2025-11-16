using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Caching.Memory;
using censudex_api.src.Models;
using censudex_api.src.Dto;

namespace censudex_api.src.Middleware
{
    public class TokenValidationInterceptor : Interceptor
    {
        private readonly HttpClient _authServiceClient;
        private readonly IMemoryCache _cache;

        public TokenValidationInterceptor(
            IHttpClientFactory httpClientFactory,
            IMemoryCache cache)
        {
            _authServiceClient = httpClientFactory.CreateClient("AuthService");
            _cache = cache;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
            TRequest request,
            ServerCallContext context,
            UnaryServerMethod<TRequest, TResponse> continuation)
        {
            var method = context.Method; // e.g., "/client.ClientService/RegisterClient"

            var publicMethods = new[]
            {
                "/client.ClientService/RegisterClient",
                "/client.ClientService/GetClient",
                "/client.ClientService/RegisterClient",
                "/auth.AuthService/Login",
                "/health"
            };

            if (publicMethods.Contains(method))
            {
                return await continuation(request, context);
            }
            
            // Extract token from metadata
            var authHeader = context.RequestHeaders
                .FirstOrDefault(h => h.Key == "authorization")?.Value;

            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid token"));
            }

            var token = authHeader.Substring(7); // Remove "Bearer "

            // Check cache first (optional optimization)
            var cacheKey = $"token_valid_{token}";
            if (_cache.TryGetValue(cacheKey, out bool isValidCached))
            {
                if (!isValidCached)
                {
                    throw new RpcException(new Status(StatusCode.Unauthenticated, "Token has been revoked"));
                }
            }
            else
            {
                // Validate with Auth Service
                var validationResponse = await _authServiceClient.GetAsync(
                    $"http://localhost:5111/api/Auth/validate",
                    new StringContent("", Encoding.UTF8, "application/json")
                    {
                        Headers = { { "Authorization", authHeader } }
                    });

                var result = await validationResponse.Content.ReadFromJsonAsync<TokenValidationResponse>();

                if (result == null || !result.IsValid)
                {
                    _cache.Set(cacheKey, false, TimeSpan.FromMinutes(1));
                    throw new RpcException(new Status(StatusCode.Unauthenticated, 
                        result?.Message ?? "Invalid token"));
                }

                // Cache valid token for 1 minute to reduce Auth Service calls
                _cache.Set(cacheKey, true, TimeSpan.FromMinutes(1));

                // Add claims to context for downstream services
                foreach (var claim in result.Claims ?? new List<ClaimDto>())
                {
                    context.RequestHeaders.Add(claim.Type, claim.Value);
                }
            }

            // Continue to the actual service
            return await continuation(request, context);
        }
    }
}