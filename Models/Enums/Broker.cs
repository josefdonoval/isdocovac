namespace Isdocovac.Models.Enums;

public enum Broker
{
    Xtb = 0,
    InteractiveBrokers = 1,
    Revolut = 2,
    Degiro = 3,
    RaiffeisenBank = 4,
}

public static class BrokerLabel
{
    public static string For(Broker broker) => broker switch
    {
        Broker.Xtb => "XTB",
        Broker.InteractiveBrokers => "Interactive Brokers",
        Broker.Revolut => "Revolut",
        Broker.Degiro => "DEGIRO",
        Broker.RaiffeisenBank => "Raiffeisen Bank",
        _ => broker.ToString(),
    };
}
