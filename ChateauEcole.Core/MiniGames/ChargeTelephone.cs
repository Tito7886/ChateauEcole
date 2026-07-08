namespace ChateauEcole.Core.MiniGames;

/// <summary>
/// Scène de charge du téléphone (bonus narratif, jamais un verrou de progression).
/// Animation comique en machine à écrire (WriteSlow), puis le téléphone est « chargé »
/// (flag telephone_charge). Le PREMIER SMS de « N. » tombe juste après, car
/// GameEngine.CheckPhoneAndSms() s'exécute après chaque action.
///
/// Le chargeur est CONSOMMÉ (laissé branché) : il n'a aucun autre usage et cela allège le
/// sac (max 8) ; le +5 de sa première prise reste acquis (flag got_chargeur).
///
/// Déclenchée par un choix intégré du moteur, disponible dans N'IMPORTE QUELLE salle dès
/// qu'on a le téléphone ET le chargeur en inventaire : impossible de rester bloqué avec un
/// chargeur sans pouvoir lancer l'animation (anti-softlock).
/// </summary>
public static class ChargeTelephone
{
    public static void Jouer(GameEngine engine, IGameIO io)
    {
        io.WriteSlow("Tu branches le chargeur sur une prise murale et tu y relies le téléphone fissuré.");
        io.WriteSlow("Le téléphone charge...");
        io.WriteSlow("Le téléphone charge encore....");
        io.WriteSlow("Le téléphone est dramatiquement toujours en train de charger....");
        io.WriteSlow("Heureusement, votre patience vous protège....");
        io.WriteSlow("Ah... oui... encore en charge...");
        io.WriteSlow("Oh...");
        io.WriteSlow("Et puis zut. 10% de batterie, ce sera bien suffisant !");

        engine.State.Flags.Add("telephone_charge");
        engine.State.Inventory.Remove("chargeur"); // laissé branché à la prise : consommé
        io.WriteLine("Tu débranches le téléphone et abandonnes le chargeur à sa prise. Repose en paix, chargeur.");
        io.Pause(); // un petit temps avant que le premier SMS ne tombe (effet « récompense »)
    }
}
