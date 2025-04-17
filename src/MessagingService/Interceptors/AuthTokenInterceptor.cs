using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using System.Linq;

namespace MessagingService.Interceptors
{
    public class AuthTokenInterceptor : Interceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuthTokenInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            // Retrieve the token from the current HTTP context, if available.
            var token = _httpContextAccessor.HttpContext?.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(token))
            {
                var metadata = new Metadata
            {
                { "Authorization", token }
            };
                var options = context.Options.WithHeaders(metadata);
                var newContext = new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, options);
                return base.AsyncUnaryCall(request, newContext, continuation);
            }
            return base.AsyncUnaryCall(request, context, continuation);
        }
    }
}