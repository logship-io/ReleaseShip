using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ReleaseShip.Data.Models;

namespace ReleaseShip.Data.Services
{
    internal sealed class ContainerTagPolicyService : IContainerTagPolicyService
    {
        public Task<bool> CanUpdateTagAsync(ContainerRepositoryDetail repository, string tagName, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(repository);

            string normalizedTag = NormalizeTag(tagName);
            bool immutable = string.Equals(repository.Repository.DefaultTagMutability, ContainerTagMutabilityModes.Immutable, StringComparison.Ordinal);
            foreach (var rule in repository.TagRules)
            {
                if (!rule.IsImmutable)
                {
                    continue;
                }

                if (CreatePatternRegex(rule.Pattern).IsMatch(normalizedTag))
                {
                    immutable = true;
                }
            }

            return Task.FromResult(!immutable);
        }

        private static string NormalizeTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
            {
                throw new ArgumentException("Tag name is required.", nameof(tagName));
            }

            return tagName.Trim();
        }

        private static Regex CreatePatternRegex(string pattern)
        {
            string escaped = Regex.Escape(pattern.Trim())
                .Replace("\\*", ".*")
                .Replace("\\?", ".");
            return new Regex($"^{escaped}$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
    }
}
