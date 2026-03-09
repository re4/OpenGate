namespace OpenGate.Application.Migration;

public class MigrationProgress
{
    public string CurrentStep { get; set; } = string.Empty;
    public int TotalSteps { get; set; } = 7;
    public int CompletedSteps { get; set; }
    public bool IsRunning { get; set; }
    public bool IsComplete { get; set; }
    public string? Error { get; set; }
    public List<MigrationStepResult> Steps { get; set; } = new();
}

public class MigrationStepResult
{
    public string Name { get; set; } = string.Empty;
    public int Imported { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public List<string> Warnings { get; set; } = new();
    public bool Success { get; set; }
}

public class PaymenterCounts
{
    public int Users { get; set; }
    public int Categories { get; set; }
    public int Products { get; set; }
    public int Orders { get; set; }
    public int Services { get; set; }
    public int Invoices { get; set; }
    public int InvoiceTransactions { get; set; }
    public int Tickets { get; set; }
    public int TicketMessages { get; set; }
}

public class ConnectionTestResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public PaymenterCounts? Counts { get; set; }
}
