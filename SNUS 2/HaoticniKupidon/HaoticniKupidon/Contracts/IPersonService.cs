using System.ServiceModel;

namespace HaoticniKupidon.Contracts
{
    [ServiceContract(CallbackContract = typeof(ICupidCallback), SessionMode = SessionMode.Required)]
    public interface IPersonService
    {
        [OperationContract]
        OperationResult InitSinglePerson(SinglePerson person);

        [OperationContract(IsOneWay = true)]
        void ConfirmLetterReceived(string username);

        [OperationContract]
        OperationResult BlockUser(string username, string usernameToBlock);
    }
}
