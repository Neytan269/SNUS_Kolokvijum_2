using System.ServiceModel;

namespace HaoticniKupidon.Contracts
{
    [ServiceContract]
    public interface ICupidCallback
    {
        [OperationContract(IsOneWay = true)]
        void ReceiveLoveLetter(LoveLetter letter);
    }
}
