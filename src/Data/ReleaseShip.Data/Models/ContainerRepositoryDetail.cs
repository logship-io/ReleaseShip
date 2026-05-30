using System.Collections.Generic;

namespace ReleaseShip.Data.Models
{
    public class ContainerRepositoryDetail
    {
        public ContainerRepository Repository { get; set; } = new();
        public IReadOnlyList<ContainerTagRule> TagRules { get; set; } = [];
    }
}
