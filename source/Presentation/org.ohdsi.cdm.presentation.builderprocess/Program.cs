﻿using System;
using System.Configuration;
using System.Text.RegularExpressions;
using org.ohdsi.cdm.builders.cprd_v5;
using org.ohdsi.cdm.builders.jmdc_v5;
using org.ohdsi.cdm.builders.optumextendedses;
using org.ohdsi.cdm.builders.optumoncology;
using org.ohdsi.cdm.builders.premier_v5;
using org.ohdsi.cdm.builders.seer;
using org.ohdsi.cdm.builders.truven_v5;
using org.ohdsi.cdm.framework.core;
using org.ohdsi.cdm.framework.core.Base;
using org.ohdsi.cdm.framework.core.Controllers;
using org.ohdsi.cdm.framework.data.DbLayer;
using org.ohdsi.cdm.framework.shared.Enums;
using org.ohdsi.cdm.builders.eras_v5;

namespace org.ohdsi.cdm.presentation.builderprocess
{
   class Program
   {
      private static int subChunkSize;
      static void Main(string[] inputArgs)
      {
         if (inputArgs.Length > 0)
         {
            var args = string.Join(" ", inputArgs);
            var builderConnectionString = Regex.Match(args, @"(?s)(?<=\<cs\>).*?(?=\<\/cs\>)", RegexOptions.IgnoreCase).Value;
            var awsAccessKeyId = Regex.Match(args, @"(?s)(?<=\<keyid\>).*?(?=\<\/keyid\>)", RegexOptions.IgnoreCase).Value;
            var awsSecretAccessKey = Regex.Match(args, @"(?s)(?<=\<accesskey\>).*?(?=\<\/accesskey\>)", RegexOptions.IgnoreCase).Value;
            var bucket = Regex.Match(args, @"(?s)(?<=\<bucket\>).*?(?=\<\/bucket\>)", RegexOptions.IgnoreCase).Value;
            subChunkSize = int.Parse(Regex.Match(args, @"(?s)(?<=\<SubChunkSize\>).*?(?=\<\/SubChunkSize\>)", RegexOptions.IgnoreCase).Value);

            Settings.Initialize(builderConnectionString, Environment.MachineName);
            Settings.Current.AwsAccessKeyId = awsAccessKeyId;
            Settings.Current.AwsSecretAccessKey = awsSecretAccessKey;
            Settings.Current.Bucket = bucket;
         }
         else
         {
            Settings.Initialize(ConfigurationManager.ConnectionStrings["Builder"].ConnectionString, Environment.MachineName);
         }
        
         var builderController = new BuilderController();
         try
         {
            var vocabulary = new Vocabulary();
            vocabulary.Initialize();

            Build(builderController);
         }
         catch (Exception e)
         {
            Logger.WriteError(e);

            builderController.UpdateState(BuilderState.Error);
         }
      }

      private static void Build(BuilderController builderController)
      {
         var dbChunk = new DbChunk(Settings.Current.Building.BuilderConnectionString);
         int? chunkId = null;
         while (true)
         {
            try
            {
               builderController.RefreshState();
               if (builderController.Builder.State == BuilderState.Stopping)
                  break;

               if (builderController.Builder.State == BuilderState.Stopped)
                  break;

               if (builderController.Builder.State == BuilderState.Error)
                  break;

               if (builderController.Builder.State == BuilderState.Unknown ||
                   builderController.Builder.State == BuilderState.Idle)
                  continue;

               chunkId = dbChunk.TakeChunk(Settings.Current.Building.Id.Value, Settings.Current.Builder.Id.Value);
               if (!chunkId.HasValue) break;

                var attempt = 0;
                var processing = false;
                var chunk = Settings.Current.Building.SourceEngine.GetChunkBuilder(chunkId.Value, CreatePersonBuilder, subChunkSize);
                while (!processing)
                {

                    try
                    {
                        attempt++;
                        chunk.Process();
                        processing = true;
                    }
                    catch (Exception ex)
                    {
                        if (attempt <= 3)
                        {
                            Logger.Write(chunkId, LogMessageTypes.Warning, "chunk.Process attempt=" + attempt + ") " + Logger.CreateExceptionString(ex));
                            chunk = Settings.Current.Building.SourceEngine.GetChunkBuilder(chunkId.Value, CreatePersonBuilder, subChunkSize);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
               chunkId = null;
            }
            catch (Exception e)
            {
               if (chunkId.HasValue)
               {
                  Logger.WriteError(chunkId, e);
                  dbChunk.ChunkFailed(chunkId.Value);
               }
               else
                  Logger.Write(null, Settings.Current.Building.Id.Value, null, LogMessageTypes.Error,
                     Logger.CreateExceptionString(e));


               builderController.UpdateState(BuilderState.Error);
            }
         }
      }

      private static IPersonBuilder CreatePersonBuilder()
      {
         switch (Settings.Current.Building.Vendor)
         {
            case Vendors.Truven_CCAE:
            case Vendors.Truven_MDCR:
            case Vendors.Truven_MDCD:
               return new Truvenv5PersonBuilder();
              
            case Vendors.CprdV5:
               return new CprdV5PersonBuilder();

            case Vendors.PremierV5:
               return new PremierV5PersonBuilder();

            case Vendors.ErasV5:
               return new ErasV5PersonBuilder();

            case Vendors.JMDCv5:
               return new Jmdcv5PersonBuilder();

            case Vendors.SEER:
               return new SeerPersonBuilder();

            case Vendors.OptumExtendedSES:
            case Vendors.OptumExtendedDOD:
               return new OptumExtendedSesPersonBuilder();

            case Vendors.OptumOncology:
               return new OptumOncologyPersonBuilder();

         }

         return new PersonBuilder();
      }
   }
}
