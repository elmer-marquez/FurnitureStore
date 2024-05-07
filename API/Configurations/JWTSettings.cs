using System;

namespace API.Configurations
{
    public class JWTSettings
    {
        public string Secret { get; set; }
        public TimeSpan ExpiryTime { get; set; }
    }
}
