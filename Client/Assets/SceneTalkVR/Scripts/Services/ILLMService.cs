using System.Threading.Tasks;
using SceneTalkVR.Core;

namespace SceneTalkVR.Core
{
    /// <summary>
    /// Service for communicating with Large Language Models.
    /// </summary>
    public interface ILLMService
    {
        /// <summary>
        /// Parses user natural language input into a structured intent JSON.
        /// </summary>
        Task<SpringScenePayload> ParseIntentAsync(string userInput);

        /// <summary>
        /// Generates a natural language reply based on the chat history.
        /// </summary>
        Task<string> GenerateReplyAsync(string chatHistory);
    }
}
