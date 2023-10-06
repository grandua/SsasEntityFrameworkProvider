using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.Entity.Core.Common;
using System.Data.Entity.Core.Common.CommandTrees;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Infrastructure.DependencyResolution;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using AgileDesign.SsasEntityFrameworkProvider.AdomdClient;
using AgileDesign.SsasEntityFrameworkProvider.Internal;
using AgileDesign.SsasEntityFrameworkProvider.Internal.MdxGeneration;
using AgileDesign.Utilities;
using Microsoft.AnalysisServices.AdomdClient;

#if ! DEBUG
using System.Diagnostics;
#endif


namespace AgileDesign.SsasEntityFrameworkProvider
{
#if ! DEBUG
    [DebuggerStepThrough]
#endif
    public class SsasProvider 
        : DbProviderServices
        //, IDbProviderFactoryResolver
    {
        public const string ProviderInvariantName = "AgileDesign.SsasEntityFrameworkProvider";

        private SsasProvider()
        {
            AddDependencyResolver(new SingletonDependencyResolver<IDbConnectionFactory>(SsasProviderFactory.Instance));

            AddDependencyResolver(
                new ExecutionStrategyResolver<TmpDefaultSqlExecutionStrategy>(
                    ProviderInvariantName, null, () => new TmpDefaultSqlExecutionStrategy()));
        }

        public static SsasProvider Instance = new SsasProvider();

        /// <summary>
        ///   Creates a command definition object for the specified provider manifest and command tree.
        /// </summary>
        /// <returns>
        ///   An executable command definition object.
        /// </returns>
        /// <param name = "providerManifest">
        /// Provider manifest previously retrieved from the store provider.
        /// </param>
        /// <param name = "commandTree">
        /// Command tree for the statement.
        /// </param>
        protected override DbCommandDefinition CreateDbCommandDefinition
            (
                DbProviderManifest providerManifest,
                DbCommandTree commandTree
            )
        {
            Contract.Requires<ArgumentNullException>(providerManifest != null);
            Contract.Requires<ArgumentException>
                (
                    providerManifest is SsasProviderManifest,
                    "The provider manifest given is not of type 'SsasProviderManifest'."
                );
            Contract.Requires<ArgumentNullException>(commandTree != null);
            Contract.Requires<ArgumentException>(commandTree is DbQueryCommandTree);

            try
            {
                var prototypeCommand = CreateCommand(providerManifest, commandTree);
                //If new properties are added into SsasCommand map them in ICloanable.Clone() inside it.
                return CreateCommandDefinition(prototypeCommand);
            }
            catch (Exception ex)
            {
                TraceError(ex);
                throw CloneExceptionAndAddLicenseUserInfo(ex);
            }
        }

        Exception CloneExceptionAndAddLicenseUserInfo(Exception ex)
        {
            try
            {
                return (Exception)Activator.CreateInstance(
                    ex.GetType()
                    , ex.Message
                    , ex);
            }
            catch (MissingMethodException)
            { // For errors like "Constructor on type 
                //'System.Diagnostics.Contracts.__ContractsRuntime+ContractException' not found."
                throw ex;
            }
        }

        /// <summary>
        ///   Create a SampleCommand object, given the provider manifest and command tree
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SqlGenParametersNotPermitted")]
        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling")]
        DbCommand CreateCommand
            (
            DbProviderManifest manifest,
            DbCommandTree commandTree
            )
        {
            IDictionary<int, int> linqToMdxColumnsOrder;
            string mdx = MdxGenerator.GenerateMdx
                (
                    commandTree
                    , ( (SsasProviderManifest)manifest ).Version
                    , out linqToMdxColumnsOrder
                );

            var command = new SsasCommand
            {
                CommandText = mdx + GetSignature(), 
                MdxColumnsOrder = linqToMdxColumnsOrder
            };
            foreach (var queryParameter in (commandTree.Parameters ?? new KeyValuePair<string, TypeUsage>[]{}))
            {
                command.Parameters.Add(CreateAdomdParameter(queryParameter.Key));
            }
            
            return command;
        }

        SsasParameter CreateAdomdParameter(string parameterName)
        {
            return new SsasParameter
                (
                    new AdomdParameter
                    {
                        ParameterName = parameterName
                    }
                );
        }

        string GetSignature()
        { 
            string licenseUserInfo = "";
            //TODO: is it allowed to send this info to Server? Can I send a company name only? -Question to a lawyer.
            //if (!string.IsNullOrWhiteSpace(MdxGenerator.License.UserNameAndCompany))
            //{
            //    licenseUserInfo = ", " + MdxGenerator.License.UserNameAndCompany;
            //}
            return string.Format("{0}-- provider: {1}{2}"
                                 , Environment.NewLine + Environment.NewLine
                                 , GetType().Assembly.FullName
                                 , licenseUserInfo);
        }

        /// <summary>
        /// For unit tests
        /// </summary>
        [Obfuscation(Exclude = true)]
        internal static string SqlServerVersion { get; set; }
        /// <summary>
        ///   Returns provider manifest token given a connection.
        /// </summary>
        /// <returns>
        ///   The provider manifest token for the specified connection.
        /// </returns>
        /// <param name = "connection">
        ///   Connection to provider.
        /// </param>
        protected override string GetDbProviderManifestToken(DbConnection connection)
        {
            return SsasProviderManifest.TokenSql10; //we do not care for other versions yet, save some performance
        }

        protected override DbProviderManifest GetDbProviderManifest(string versionHint)
        {
            Contract.Requires<ArgumentException>
                (
                    ! string.IsNullOrWhiteSpace(versionHint)
                    , "Could not determine store version; a valid store connection or a version hint is required.");

            try
            { 
                return new SsasProviderManifest(versionHint);
            }
            catch (Exception ex)
            {
                TraceError(ex);
                throw CloneExceptionAndAddLicenseUserInfo(ex);
            }
        }

        void TraceError(Exception ex)
        {
            Logger.TraceError(ex.ToString());
        }

        //public DbProviderFactory ResolveProviderFactory(DbConnection connection)
        //{
        //    return SsasProviderFactory.Instance;
        //}
    }
}