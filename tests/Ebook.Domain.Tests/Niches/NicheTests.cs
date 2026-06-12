using Ebook.Domain.Niches;

namespace Ebook.Domain.Tests.Niches;

public class NicheTests
{
    private static readonly DateTime Now = new(2026, 6, 11, 12, 0, 0, DateTimeKind.Utc);

    private static Niche NewNiche() =>
        Niche.Discover("financas-autonomos", "Finanças para Autônomos", 0.82, "{}", 1, Now);

    [Fact]
    public void Discover_inicia_candidate_e_emite_evento()
    {
        var niche = NewNiche();

        Assert.Equal(NicheStatus.Candidate, niche.Status);
        Assert.Single(niche.DomainEvents.OfType<NicheDiscovered>());
    }

    [Fact]
    public void Select_emite_NicheSelected_que_dispara_o_pipeline()
    {
        var niche = NewNiche();

        Assert.True(niche.Select().IsSuccess);
        Assert.Equal(NicheStatus.Selected, niche.Status);
        Assert.Single(niche.DomainEvents.OfType<NicheSelected>());
    }

    [Fact]
    public void Select_duas_vezes_falha()
    {
        var niche = NewNiche();
        niche.Select();

        Assert.True(niche.Select().IsFailure);
    }

    [Fact]
    public void Activate_exige_selected()
    {
        var niche = NewNiche();
        Assert.True(niche.Activate().IsFailure);

        niche.Select();
        Assert.True(niche.Activate().IsSuccess);
        Assert.Equal(NicheStatus.Active, niche.Status);
    }

    [Fact]
    public void Discard_de_active_falha()
    {
        var niche = NewNiche();
        niche.Select();
        niche.Activate();

        Assert.True(niche.Discard().IsFailure);
    }
}
