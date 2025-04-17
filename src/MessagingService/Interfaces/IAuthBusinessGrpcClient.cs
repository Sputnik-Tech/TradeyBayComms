using System.Threading.Tasks;
using TradeyBay.Auth; // Proto-generated types for Auth

namespace MessagingService.Interfaces
{
    public interface IAuthBusinessGrpcClient
    {
        Task<RegisterBusinessResponse> RegisterBusinessAsync(RegisterBusinessRequest request);
        Task<GetBusinessResponse> GetBusinessAsync(GetBusinessRequest request);
        Task<UpdateBusinessResponse> UpdateBusinessAsync(UpdateBusinessRequest request);
        Task<DeleteBusinessResponse> DeleteBusinessAsync(DeleteBusinessRequest request);
        Task<UpdateBusinessLocationResponse> UpdateBusinessLocationAsync(UpdateBusinessLocationRequest request);
        Task<GetBusinessesByUserResponse> GetBusinessesByUserAsync(GetBusinessesByUserRequest request);
    }
}
