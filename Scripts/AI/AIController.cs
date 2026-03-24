using Godot;
using System.Collections.Generic;

public partial class AIController : Node
{
    public string AILevel { get; set; } = "Easy"; // "Easy", "Medium", "Hard"

    private float _decisionTimer = 0f;
    private float _attackTimer = 0f;

    // Intervalle de décision selon le niveau
    private float DecisionInterval => AILevel switch
    {
        "Hard" => 1.5f,
        "Medium" => 3f,
        _ => 5f // Easy
    };

    private float AttackInterval => AILevel switch
    {
        "Hard" => 2f,
        "Medium" => 4f,
        _ => 7f
    };

    public override void _Process(double delta)
    {
        _decisionTimer += (float)delta;
        if (_decisionTimer >= DecisionInterval)
        {
            _decisionTimer = 0f;
            TryBuyUnits();
        }

        _attackTimer += (float)delta;
        if (_attackTimer >= AttackInterval)
        {
            _attackTimer = 0f;
            SendUnitsToAttack();
        }
    }

    private void TryBuyUnits()
    {
        if (GameManager.Instance == null) return;

        var botTeams = GameManager.Instance.GetBotTeamIds();
        if (botTeams.Count == 0) return;

        foreach (var teamId in botTeams)
        {
            int gold = GameManager.Instance.GetGold(teamId);
            int tier = GameManager.Instance.GetUnlockedTier(teamId);

            // Trouver les camps de cette équipe
            foreach (var node in GetTree().GetNodesInGroup("camps"))
            {
                if (node is not CampSimple camp) continue;
                if (camp.GetTeamId() != teamId) continue;
                if (camp.IsNeutralCamp) continue;
                if (camp.GetQueueCount() >= camp.GetMaxQueueSize()) continue;

                string unitToBuy = ChooseUnit(gold, tier);
                if (unitToBuy != null && camp.CanBuyUnit(unitToBuy))
                    camp.BuyUnit(unitToBuy);
            }
        }
    }

    private string ChooseUnit(int gold, int tier)
    {
        return AILevel switch
        {
            "Hard" => ChooseHardUnit(gold, tier),
            "Medium" => ChooseMediumUnit(gold, tier),
            _ => ChooseEasyUnit(gold)
        };
    }

    private string ChooseEasyUnit(int gold)
    {
        if (gold >= 80) return "Range";
        if (gold >= 75) return "Support";
        if (gold >= 50) return "Infantry";
        return null;
    }

    private string ChooseMediumUnit(int gold, int tier)
    {
        if (tier >= 2 && gold >= 120) return "AntiArmor";
        if (gold >= 80) return "Range";
        if (gold >= 75) return "Support";
        if (gold >= 50) return "Infantry";
        return null;
    }

    private string ChooseHardUnit(int gold, int tier)
    {
        if (tier >= 3 && gold >= 200) return "Tank";
        if (tier >= 3 && gold >= 150) return "Heavy";
        if (tier >= 3 && gold >= 130) return "Mortar";
        if (tier >= 2 && gold >= 120) return "AntiArmor";
        if (gold >= 100) return "Heal";
        if (gold >= 80) return "Range";
        if (gold >= 50) return "Infantry";
        return null;
    }

    private void SendUnitsToAttack()
    {
        if (GameManager.Instance == null) return;

        var botTeams = GameManager.Instance.GetBotTeamIds();
        if (botTeams.Count == 0) return;

        // Trouver les camps ennemis (team 1)
        var enemyCamps = new List<CampSimple>();
        foreach (var node in GetTree().GetNodesInGroup("camps"))
        {
            if (node is CampSimple camp && camp.GetTeamId() == 1)
                enemyCamps.Add(camp);
        }
        if (enemyCamps.Count == 0) return;

        foreach (var teamId in botTeams)
        {
            // Trouver les unités idle de cette équipe
            foreach (var node in GetTree().GetNodesInGroup("units"))
            {
                if (node is not Unit unit) continue;
                if (unit.TeamId != teamId) continue;
                if (!unit.IsIdleState()) continue;

                // Cibler le camp ennemi le plus proche
                CampSimple target = FindNearestEnemyCamp(unit.GlobalPosition, enemyCamps);
                if (target != null)
                    unit.MoveTo(target.GlobalPosition);
            }
        }
    }

    private CampSimple FindNearestEnemyCamp(Vector2 from, List<CampSimple> camps)
    {
        CampSimple nearest = null;
        float minDist = float.MaxValue;
        foreach (var camp in camps)
        {
            float dist = from.DistanceTo(camp.GlobalPosition);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = camp;
            }
        }
        return nearest;
    }
}
