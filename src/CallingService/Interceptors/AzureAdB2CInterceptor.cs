using Grpc.Core;
using Grpc.Core.Interceptors;

namespace CallingService.Interceptors;

public class AzureADB2CAuthInterceptor : Interceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AzureADB2CAuthInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
         TRequest request,
         ClientInterceptorContext<TRequest, TResponse> context,
         AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
         // Example: Log the authorization header if present.
         var httpContext = _httpContextAccessor.HttpContext;
         if (httpContext != null && httpContext.Request.Headers.ContainsKey("Authorization"))
         {
             var token = httpContext.Request.Headers["Authorization"].ToString();
             // You can log or validate the token here.
         }
         // Continue with the call.
         return base.AsyncUnaryCall(request, context, continuation);
    }
}

