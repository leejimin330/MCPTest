namespace FarmCreature.Core
{
    /// <summary>육성 액션의 결과. Message 는 UI 로그에 그대로 표시된다.</summary>
    public readonly struct ActionResult
    {
        public bool Success { get; }
        public string Message { get; }

        private ActionResult(bool success, string message)
        {
            Success = success;
            Message = message;
        }

        public static ActionResult Ok(string message)
        {
            return new ActionResult(true, message);
        }

        public static ActionResult Fail(string message)
        {
            return new ActionResult(false, message);
        }
    }
}
