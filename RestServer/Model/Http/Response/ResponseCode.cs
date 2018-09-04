using Newtonsoft.Json;

namespace RestServer.Model.Http.Response
{
    /// <summary>
    /// Represents response codes for use in <see cref="ResponseBody"/>.
    /// </summary>
    /// <remarks>
    /// Public only because of Razor View compilation. Do not use outside the assembly.
    /// </remarks>
    public enum ResponseCode : int
    {
        /// <summary>
        /// Unspecified response code. Is the default.
        /// </summary>
        Unspecified = 0,
        /// <summary>
        /// The resource was not found.
        /// </summary>
        NotFound = 1,
        /// <summary>
        /// An incorrect password was provided.
        /// </summary>
        IncorrectPassword = 2,
        /// <summary>
        /// Generic success code, for operations too meaningless to have their own code here.
        /// </summary>
        GenericSuccess = 3,
        /// <summary>
        /// Generic failure code, for operations too meaningless to have their own code here.
        /// </summary>
        GenericFailure = 4,
        /// <summary>
        /// The resource that was tried to be created already exists.
        /// </summary>
        AlreadyExists = 5,
        /// <summary>
        /// The e-mail is unconfirmed for the user.
        /// </summary>
        UnconfirmedEmail = 6,
        /// <summary>
        /// The user is not authenticated, but the operation requires it to be.
        /// </summary>
        RequiresAuthentication = 7,
        /// <summary>
        /// A validation failed while validating the data that was sent by the client.
        /// </summary>
        ValidationFailure = 8,
        /// <summary>
        /// The user is authenticated, but the operation requires it not to be.
        /// </summary>
        RequiresUnauthentication = 9,
        /// <summary>
        /// A premium level higher than the current level is required for this operation to take place.
        /// </summary>
        RequiresPremium = 10,
        /// <summary>
        /// The supplied image is not valid for the required purposes.
        /// </summary>
        InvalidImage = 11,
        /// <summary>
        /// The token provided for authentication is invalid.
        /// </summary>
        IncorrectAuthenticationToken = 12,
    }
}
