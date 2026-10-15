namespace ChooGuard.Seeded.Unity
{
    /// <summary>Product UI callbacks. The panel never receives a capability or provider credential.</summary>
    public interface INativeProviderMenuHost
    {
        NativeProviderState ProviderState {get;}
        NativeAuthFlow ActiveProviderLogin {get;}
        bool ProviderMenuOpen {get;}
        bool ProviderBusy {get;}
        bool CanChangeProvider {get;}
        string ProviderMessage {get;}
        void SetProviderMenuOpen(bool open);
        void RefreshProviders();
        void RefreshProviderModels();
        void StartProviderLogin(string providerId,string accountId=null,bool reconsent=false);
        void PollProviderLogin();
        void CancelProviderLogin();
        void SelectProviderModel(NativeProviderModel model);
        void SetProviderSubscriptionEnabled(bool enabled);
        void ArmProviderInitialOnce(long inputUnits=192000,long outputTokens=2048);
        void LogoutProviderAccount(string accountId);
    }
}
