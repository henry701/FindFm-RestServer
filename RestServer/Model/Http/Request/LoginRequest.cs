namespace RestServer.Http.Request
{
    /// <summary>
    /// Request model for REST Login Request
    /// </summary>
    internal class LoginRequest
    {
        /// <summary>
        /// The e-mail for the <see cref="Models.User"/> account
        /// </summary>
        public string Email { get; set; }
        /// <summary>
        /// The password for the <see cref="Models.User"/> account
        /// </summary>
        public string Senha { get; set; }
    }
}