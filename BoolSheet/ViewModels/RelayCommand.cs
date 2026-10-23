using System.Windows.Input;

namespace BoolSheet.ViewModels;

/// <summary>
/// A minimal <see cref="ICommand"/> that just runs an action. Used instead of MAUI's <c>Command</c>
/// so that the view models do not depend on MAUI and can be compiled into the test project.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

    // The commands here are always available, so the event is never raised.
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _execute();
}
