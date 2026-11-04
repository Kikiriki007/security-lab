using Platform.Api.Domain.Identity;

namespace Platform.Api.Services.Identity;

public interface IAccountRepository
{
    UserAccount? Find(Guid id);
    void Save(UserAccount account);
}