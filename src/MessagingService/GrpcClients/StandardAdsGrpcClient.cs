using System;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes; // Required for StringValue
using Grpc.Core;
using MessagingService.ExternalDTOs;
using MessagingService.Interfaces;
using MessagingService.Mappers;
using Microsoft.Extensions.Logging;
using TradeyBay.StandardAds; // Proto-generated namespace

namespace MessagingService.GrpcClients
{
    public class StandardAdsGrpcClient : IStandardAdsGrpcClient
    {
        private readonly StandardAdService.StandardAdServiceClient _client;
        private readonly StandardAdMapper _mapper;
        private readonly ILogger<StandardAdsGrpcClient> _logger;

        public StandardAdsGrpcClient(
            StandardAdService.StandardAdServiceClient client,
            StandardAdMapper mapper,
            ILogger<StandardAdsGrpcClient> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        public async Task<StandardAdDTO?> GetAdByIdAsync(string adId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(adId))
            {
                _logger.LogWarning("GetAdByIdAsync called with null or empty adId.");
                return null;
            }

            _logger.LogDebug("Calling GetAdByIdAsync for AdId: {AdId}", adId);
            try
            {
                // Prepare the request using Google.Protobuf.WellKnownTypes.StringValue
                var request = new StringValue { Value = adId };

                // Make the gRPC call
                var protoResponse = await _client.GetAdByIdAsync(request, cancellationToken: cancellationToken);

                if (protoResponse == null)
                {
                    _logger.LogWarning("GetAdByIdAsync for AdId: {AdId} returned null response.", adId);
                    return null; // Or handle as NotFound if appropriate
                }

                _logger.LogDebug("Successfully received ad details for AdId: {AdId}. SellerId: {SellerId}, SellerType: {SellerType}",
                    adId, protoResponse.SellerId, protoResponse.SellerType);

                // Map the proto response to our local DTO
                return _mapper.FromProto(protoResponse);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                _logger.LogWarning("Ad with Id: {AdId} not found via gRPC call. Status: {StatusCode}", adId, ex.StatusCode);
                return null; // Return null when the ad is not found
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "RpcException occurred while calling GetAdByIdAsync for AdId: {AdId}. Status: {StatusCode}", adId, ex.StatusCode);
                // Re-throw or handle other specific statuses if needed
                throw; // Re-throw other gRPC errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected exception occurred while calling GetAdByIdAsync for AdId: {AdId}", adId);
                throw; // Re-throw general exceptions
            }
        }
    }
}