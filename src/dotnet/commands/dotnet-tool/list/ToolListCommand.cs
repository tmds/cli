// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.List
{
    internal delegate IToolPackageStore CreateToolPackageStore(DirectoryPath? nonGlobalLocation = null);

    internal class ListToolCommand : CommandBase
    {
        public const string CommandDelimiter = ", ";
        private readonly AppliedOption _options;
        private readonly IReporter _reporter;
        private readonly IReporter _errorReporter;
        private CreateToolPackageStore _createToolPackageStore;

        public ListToolCommand(
            AppliedOption options,
            ParseResult result,
            CreateToolPackageStore createToolPackageStore = null,
            IReporter reporter = null)
            : base(result)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _reporter = reporter ?? Reporter.Output;
            _errorReporter = reporter ?? Reporter.Error;
            _createToolPackageStore = createToolPackageStore ?? ToolPackageFactory.CreateToolPackageStore;
        }

        public override int Execute()
        {
            var global = _options.ValueOrDefault<bool>("global");
            var toolPathOption = _options.ValueOrDefault<string>("tool-path");

            DirectoryPath? toolPath = null;
            if (!string.IsNullOrWhiteSpace(toolPathOption))
            {
                toolPath = new DirectoryPath(toolPathOption);
            }

            if (toolPath == null && !global)
            {
                throw new GracefulException(LocalizableStrings.NeedGlobalOrToolPath);
            }

            if (toolPath != null && global)
            {
                throw new GracefulException(LocalizableStrings.GlobalAndToolPathConflict);
            }

            var table = new PrintableTable<IToolPackage>();

            table.AddColumn(
                LocalizableStrings.PackageIdColumn,
                p => p.Id.ToString());
            table.AddColumn(
                LocalizableStrings.VersionColumn,
                p => p.Version.ToNormalizedString());
            table.AddColumn(
                LocalizableStrings.CommandsColumn,
                p => string.Join(CommandDelimiter, p.Commands.Select(c => c.Name)));

            table.PrintRows(GetPackages(toolPath), l => _reporter.WriteLine(l));
            return 0;
        }

        private IEnumerable<IToolPackage> GetPackages(DirectoryPath? toolPath)
        {
            return _createToolPackageStore(toolPath).EnumeratePackages()
                .Where(PackageHasCommands)
                .OrderBy(p => p.Id)
                .ToArray();
        }

        private bool PackageHasCommands(IToolPackage package)
        {
            try
            {
                // Attempt to read the commands collection
                // If it fails, print a warning and treat as no commands
                return package.Commands.Count >= 0;
            }
            catch (Exception ex) when (ex is ToolConfigurationException)
            {
                _errorReporter.WriteLine(
                    string.Format(
                        LocalizableStrings.InvalidPackageWarning,
                        package.Id,
                        ex.Message).Yellow());
                return false;
            }
        }
    }
}
