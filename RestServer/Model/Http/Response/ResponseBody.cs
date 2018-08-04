namespace RestServer.Model.Http.Response
{
    /// <summary>
    /// Represents the response body for AJAX requests, and may be used inside the ViewBag to carry
    /// additional data for the view after a HTTP POST operation.
    /// </summary>
    /// <remarks>Public only because of Razor View compilation. Do not use outside the assembly.</remarks>
    public class ResponseBody
    {
        /// <summary>
        /// Whether the request was successfully processed or not.
        /// </summary>
        public bool Success { get; set; }
        /// <inheritdoc cref="ResponseCode"/>
        public ResponseCode Code { get; set; }
        /// <summary>
        /// A message to be shown to the user.
        /// </summary>
        public string Message { get; set; }
        /// <summary>
        /// Any additional data to be included in the response body.
        /// </summary>
        /// <see langword="dynamic"/>
        public dynamic Data { get; set; }
    }
}
