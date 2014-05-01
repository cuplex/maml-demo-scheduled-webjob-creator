
namespace ScheduledWebJobCreator
{
    /// <summary>
    /// Provides the basic data points needed to support AAD 
    /// authentication in an app making use of the 
    /// management libraries.
    /// </summary>
    public interface ITokenCredentialConfiguration
    {
        string GetTenantId();
        string GetClientId();
        string GetRedirectUrl();
    }

    /// <summary>
    /// My personal configuration. :)
    /// </summary>
    public class MyPersonalConfiguration : ITokenCredentialConfiguration
    {
        private string _tenantId;
        private string _clientId;
        private string _redirectUrl;

        public MyPersonalConfiguration(ScheduledWebJobCreatorParameters parameters)
        {
            _tenantId = parameters.activeDirectoryTenantId;
            _clientId = parameters.activeDirectoryClientId;
            _redirectUrl = parameters.activeDirectoryRedirectUrl;
        }

        public string GetTenantId()
        {
            return _tenantId;
        }

        public string GetClientId()
        {
            return _clientId;
        }

        public string GetRedirectUrl()
        {
            return _redirectUrl;
        }
    }
}
