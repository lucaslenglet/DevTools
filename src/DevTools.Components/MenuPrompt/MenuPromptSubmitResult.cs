namespace DevTools.Components.MenuPrompt;

public record MenuPromptSubmitResult<T>(T Data, ConsoleKeyInfo ConsoleKeyInfo, int OptionIndex);