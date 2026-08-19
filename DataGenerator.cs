using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.ExceptionServices;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using ZstdSharp;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace DataGenerator
{

    class DataGenerator
    {
        // seed specific data
        public static int seed;
        public static int dunType;
        public static int scrapAmount;
        public static System.Random AnomalyRandom = new System.Random();
        public static System.Random LevelRandom = new System.Random();
        public static System.Random EnemySpawnRandom = new System.Random();
        public static System.Random DaytimeEnemySpawnRandom = new System.Random();
        public static System.Random resultRandom = new System.Random();
        public static List<int> scrapToSpawn = new List<int>();
        public static int typeSID = -1;
        public static List<int> scrapValues = new List<int>();
        public static int scrapTotal;
        public static int rushType = 0;
        public static int indoorFog = 0;
        public static int cycleCheck = -1;
        public static int currentHour = 2; // 2 corresponds to the first enemy wave
        public static int enemyWave = 1;
        public static int[] daytimeSpawned = new int[5];
        public static float currentDaytimeEnemyPower = 0f;
        public static List<int> spawnedDaytimeEnemies = new List<int>();
        public static int[] beeCloseValues = new int[6];
        public static int[] beeFarValues = new int[6];
        public static int[] eggCloseValues = new int[3];
        public static int[] eggFarValues = new int[3];
        public static int dayPotential = 0;
        public static int numCycle = 0;
        public static bool firstTimeSpawningDaytimeEnemies = true;

        // moon specific data
        public static int scrapSum = 0;
        public static int scrapLength = 0;
        public static bool doNutSpawns = false;
        public static bool doBugSpawns = false;
        public static bool doDayScrap = false;
        public static int countBits = 0;
        public static List<int> scrapFileSizes = new List<int>(); // maximum is 4 long

        // data regarding outdoor and indoor enemies has not been updated to v81 since they are not yet used in this generator


        public static IMoon currentlevel = new Experimentation();

        public static List<IMoon> moonList =
        [
            new Experimentation(),
            new Assurance(),
            new Vow(),
            new Offense(),
            new March(),
            new Adamance(),
            new Rend(),
            new Dine(),
            new Titan(),
            new Embrion(),
            new Artifice()
        ];

        static void Main()
        {
            foreach (IMoon moon in moonList)
            {
                currentlevel = moon;

                Console.WriteLine($"Preparing Moon {currentlevel.moonName}");

                if (!PrepareMoon())
                {
                    Console.WriteLine($"ERROR: Moon {currentlevel.moonName} needs more scrap mixes. Skipping...");
                    continue;
                }

                Console.WriteLine($"Scrap Sum: {scrapSum}. doNutSpawns: {doNutSpawns}. doBugSpawns: {doBugSpawns}. doDayScrap: {doDayScrap}");
                Console.WriteLine($"Starting data generation for Moon: {currentlevel.moonName}.");
                Searcher(99999999); // 99999999 is max
            }



        }

        public static bool PrepareMoon()
        {
            if (Array.IndexOf(currentlevel.scrapType, (int)Scrap.gift) != -1)
            {
                //currentlevel.scrapRarity[Array.IndexOf(currentlevel.scrapType, (int)Scrap.gift)] += 30; // Before v81, gift boxes would get +30 rarity weight
            }

            scrapSum = currentlevel.scrapRarity.Sum(); // lethal company reruns this sum for every scrap, so this is just more efficient

            doNutSpawns = false;
            if (Array.IndexOf(currentlevel.indoorEnemiesType, (int)IndoorEnemies.nutcracker) != -1)
            {
                doNutSpawns = true;
            }

            doBugSpawns = false;
            if (Array.IndexOf(currentlevel.indoorEnemiesType, (int)IndoorEnemies.hoardingBug) != -1)
            {
                doBugSpawns = true;
            }

            doDayScrap = false;
            if (Array.IndexOf(currentlevel.daytimeEnemiesType, (int)DaytimeEnemies.bee) != -1 || Array.IndexOf(currentlevel.daytimeEnemiesType, (int)DaytimeEnemies.kiwi) != -1)
            {
                doDayScrap = true;
            }

            scrapLength = currentlevel.scrapType.Length;

            countBits = 4; // scrap count size
            if (currentlevel.numMax > 120)
            {
                countBits = 7; // Dine
            }
            else if (currentlevel.numMax > 29)
            {
                countBits = 4; // Titan and Artifice ; it is unlikely that these moons will exceed the limit (when ignoring SIDs and cycles)
            }

            // decide file logic
            int totalBits = scrapLength * countBits;
            int bigFiles = 0;
            int bigLimit = (int)(64 / countBits) * countBits;
            int intLimit = (int)(32 / countBits) * countBits; // maximum amount of bits able to be stored based on bit size of scrap type count
            int smallLimit = (int)(16 / countBits) * countBits;

            while (totalBits > bigLimit)
            {
                totalBits -= bigLimit;
                bigFiles++;
            }
            if (totalBits > smallLimit + intLimit) // if greater than 48, bigint should be used anyways
            {
                totalBits = 0;
                bigFiles++;
            }

            scrapFileSizes.Clear();
            for (int i = 0; i < bigFiles; i++)
            {
                scrapFileSizes.Add(bigLimit);
            }
            if (totalBits > intLimit)
            {
                scrapFileSizes.Add(intLimit);
                scrapFileSizes.Add(smallLimit);
            }
            else if (totalBits > smallLimit)
            {
                scrapFileSizes.Add(intLimit);
            }
            else if (totalBits > 0)
            {
                scrapFileSizes.Add(smallLimit);
            }

            Console.WriteLine($"countBits: {countBits}. totalBits: {scrapLength * countBits}. File Setup:");
            for (int i = 0; i < scrapFileSizes.Count(); i++)
            {
                Console.WriteLine($"{scrapFileSizes[i]}");
            }
            if (scrapFileSizes.Count() > 4)
            {
                return false;
            }

            return true;
        }

        public static bool SpawnRandomDaytimeEnemy()
        {
            List<int> spawnProbabilities = new List<int>();
            int num = 0;
            for (int i = 0; i < currentlevel.daytimeEnemiesRarity.Length; i++)
            {
                int enemyType = currentlevel.daytimeEnemiesType[i];
                if (firstTimeSpawningDaytimeEnemies)
                {
                    daytimeSpawned[enemyType] = 0;
                }
                if (MoonData.daytimeEnemiesPower[enemyType] > (float)currentlevel.daytimePower - currentDaytimeEnemyPower || daytimeSpawned[enemyType] >= MoonData.daytimeEnemiesMaxSpawn[enemyType])// || enemyType.normalizedTimeInDayToLeave < TimeOfDay.Instance.normalizedTimeOfDay || enemyType.spawningDisabled)
                {
                    spawnProbabilities.Add(0);
                    continue;
                }
                int num2 = (int)((float)currentlevel.daytimeEnemiesRarity[i] * MoonData.daytimeEnemyCurve[enemyType][enemyWave - 1]);
                spawnProbabilities.Add(num2);
                num += num2;
            }
            firstTimeSpawningDaytimeEnemies = false;
            if (num <= 0)
            {
                return false;
            }

            int randomWeightedIndex = currentlevel.daytimeEnemiesType[GetRandomWeightedIndex(num, spawnProbabilities.ToArray(), DaytimeEnemySpawnRandom)]; // before v80, used EnemySpawnRandom
            // returns universal index of daytime enemy

            int numberToSpawn = 1;
            if (randomWeightedIndex == 3) // index 3: tuilip snakes spawn 4 at a time for a total power level of 2
            {
                numberToSpawn = 4;
            }
            for (int i = 0; i < numberToSpawn; i++)
            {
                if (MoonData.daytimeEnemiesPower[randomWeightedIndex] > (float)currentlevel.daytimePower - currentDaytimeEnemyPower)
                {
                    break; // only occurs if 4 tuilip snakes try to spawn when the remaining power is less than 2.
                }
                currentDaytimeEnemyPower += MoonData.daytimeEnemiesPower[randomWeightedIndex];
                daytimeSpawned[randomWeightedIndex] += 1;
            }

            return true;

        }

        public static void SpawnDaytimeEnemies()
        {
            if (!doDayScrap || currentDaytimeEnemyPower >= (float)currentlevel.daytimePower)
            {
                return;
            }

            float num = 60f * (float)currentHour;
            float num2 = currentlevel.daytimeEnemySpawnChanceThroughDay[enemyWave - 1];
            int num3 = Math.Clamp(DaytimeEnemySpawnRandom.Next((int)(num2 - currentlevel.daytimeEnemiesProbabilityRange), (int)(num2 + currentlevel.daytimeEnemiesProbabilityRange)), 0, 20);
            
            if (enemyWave == 1) { firstTimeSpawningDaytimeEnemies = true; }
            for (int i = 0; i < num3; i++)
            {
                if (!SpawnRandomDaytimeEnemy())
                {
                    break;
                }

            }
            if (firstTimeSpawningDaytimeEnemies) // if no enemies spawn (num3 == 0), the spawn counts will not have reset; this matters only for determining handleDaytimeScrap
            {
                for (int i = 0; i < 5; i++) // this is not ran at the beginning in case multiple waves are simulated
                {
                    daytimeSpawned[i] = 0;
                }
            }


            handleDaytimeScrap(); 
        }

        public static void handleDaytimeScrap() // custom function to represent several functions in lethal company
        {
            beeCloseValues = [0, 0, 0, 0, 0, 0];
            beeFarValues = [0, 0, 0, 0, 0, 0];
            eggCloseValues = [0, 0, 0];
            eggFarValues = [0, 0, 0];
            dayPotential = 0;
            for (int i = 0; i < daytimeSpawned[0]; i++) // evaluate bee prices
            {
                System.Random random = new System.Random(seed + 1314 + daytimeSpawned[0]);
                GetRandomNavMeshPositionInBoxPredictable(random);
                beeFarValues[i] = random.Next(50, 150);
                random = new System.Random(seed + 1314 + daytimeSpawned[0]);
                GetRandomNavMeshPositionInBoxPredictable(random);
                beeCloseValues[i] = random.Next(40, 100);
                scrapTotal += beeCloseValues[i];
                dayPotential += beeFarValues[i] - beeCloseValues[i]; // price is the same for each bee (bugged); array is used as a placeholder for intended behavior
            }
            if (daytimeSpawned[4] > 0) // evaluate kiwi egg prices
            {
                System.Random random = new System.Random(seed + 1316 + 1);
                for (int i = 0; i < 3; i++)
                {
                    eggCloseValues[i] = random.Next(40, 70);
                }
                random = new System.Random(seed + 1316 + 1);
                for (int i = 0; i < 3; i++)
                {
                    eggFarValues[i] = random.Next(70, 200);
                    scrapTotal += eggCloseValues[i];
                    dayPotential += eggFarValues[i] - eggCloseValues[i];
                }
            }
        }
        public static void EvaluateScrapValues()
        {

            scrapValues.Clear();
            scrapTotal = 0;
            int l;
            bool flag = (dunType != 2);
            for (l = 0; l < scrapAmount; l++)
            {
                int num = AnomalyRandom.Next(0, 1000);
                
                // Otherwise, the average scrap value is used since prediction is locked behind dungeon generation

                if (!flag) // GetRandomNavMeshPositionInBoxPredictable is only called for when a scrap chooses a non-parent spawner. Mineshafts have no special (hasParent) spawners, so this is always called.
                {
                    GetRandomNavMeshPositionInBoxPredictable(AnomalyRandom);
                }

                if (typeSID != -1)
                {
                    if (flag)
                    {
                        scrapValues.Add(MoonData.scrapAveSIDValue[scrapToSpawn[l]]);
                    }
                    else // since anomaly is used an unknown number of times by GetRandomNavMeshPositionInBoxPredictable when not Mineshaft, the next usages cannot be predicted
                    {
                        scrapValues.Add(Math.Clamp((int)((float)AnomalyRandom.Next(MoonData.scrapMinValue[scrapToSpawn[l]], MoonData.scrapMaxValue[scrapToSpawn[l]] + 1) * MoonData.scrapValueMultiplier), 50, 170));
                    }
                }
                else
                {
                    if (flag)
                    {
                        scrapValues.Add(MoonData.scrapAveValue[scrapToSpawn[l]]);
                    }
                    else
                    {
                        scrapValues.Add((int)((float)AnomalyRandom.Next(MoonData.scrapMinValue[scrapToSpawn[l]], MoonData.scrapMaxValue[scrapToSpawn[l]] + 1) * MoonData.scrapValueMultiplier));
                    }

                }
                if (scrapToSpawn[l] == 22 && typeSID == -1) // open gifts always for profit
                {
                    scrapValues[l] = currentlevel.scrapAveGift;
                }

                scrapTotal += scrapValues[l];
            }

            if (typeSID == -1)
            {
                // do nothing
            }
            else if (currentlevel.scrapType[typeSID] == 22 && currentlevel.scrapAveGift > 50)
            {
                // open gifts, even on sid days if not experimentation (note that gifts are usually boosted on experimentation, making them 69c instead; regardless of that boost, they should never be opened on exp. SIDs)

                scrapTotal = 0;
                for (int num7 = 0; num7 < scrapAmount; num7++)
                {
                    scrapValues[num7] = currentlevel.scrapAveGift;
                    scrapTotal += scrapValues[num7];
                }

            }
            else if (!flag)
            {
                float num5 = 600f;
                if (MoonData.twoHandedScrap.IndexOf(currentlevel.scrapType[typeSID]) != -1)
                {
                    num5 = 1500f;
                }
                if (scrapTotal > 4500)
                {
                    scrapTotal = 0;
                    for (int num6 = 0; num6 < scrapAmount; num6++)
                    {
                        scrapValues[num6] = (int)((float)scrapValues[num6] * 0.7f - 0.0001f); // for whatever reason, there is a floating point error that normally occurs (this code cannot reproduce it, so it is simulated by the - 0.0001f)
                        scrapTotal += scrapValues[num6]; // apart from the - 0.0001f; this is nearly identical to the in-game code; I do not know why a floating point error occurs in-game but not with this code
                    }

                }
                else if ((float)scrapTotal < num5)
                {
                    scrapTotal = 0;
                    for (int num7 = 0; num7 < scrapAmount; num7++)
                    {
                        scrapValues[num7] = (int)((float)scrapValues[num7] * 1.4f - 0.00001f);
                        scrapTotal += scrapValues[num7];
                    }
                }
            }
            else // calculations for average sid value using raw price values (hence 600, 1500, 4500 -> 1500, 3750, 11250)
            {

                int num1a = MoonData.scrapMinValue[currentlevel.scrapType[typeSID]];
                int num2a = MoonData.scrapMaxValue[currentlevel.scrapType[typeSID]];
                int num1b = scrapAmount * Math.Clamp(num1a, 125, 425); // clamps to possible SID prices
                int num2b = scrapAmount * Math.Clamp(num2a, 125, 425);
                int num5 = 1500;
                if (MoonData.twoHandedScrap.IndexOf(currentlevel.scrapType[typeSID]) != -1)
                {
                    num5 = 3750;
                }
                if (num1b > 11250) // if minimum possible price exceeds upper limit, average is simply reduced since it always will be
                {
                    scrapTotal = 0;
                    for (int num6 = 0; num6 < scrapAmount; num6++)
                    {
                        scrapValues[num6] = (int)((float)scrapValues[num6] * 0.7f - 0.0001f);
                        scrapTotal += scrapValues[num6];
                    }
                }
                else if (num2b < num5) // if maximum possible price is below lower limit, average is simply boosted since it always will be ; Only happens on experimentation
                {
                    scrapTotal = 0;

                    for (int num7 = 0; num7 < scrapAmount; num7++)
                    {
                        scrapValues[num7] = (int)((float)scrapValues[num7] * 1.4f - 0.0001f);
                        scrapTotal += scrapValues[num7];
                    }
                }
                else if (num2b > 11250) // if scrap can sometimes go over upper limit, average is reduced slightly
                {
                    // raw value is an integer in an interval, so that interval is divided into sections to be used as weights // SECTIONS: causes_reduction[>=425, <425], doesnt_cause_reduction[<125, >=125]

                    int n = 11250 / scrapAmount + 1; // minimum scrap value that causes reduction (+ 1 ensures that the value n will exceed the reduction requirement of 11250, not just meet it)
                    int interval1 = Math.Max(num2a, 425) - 425; // weight of possible values >=425 but <num2a (since upper bounds are exclusive)
                    int interval2 = Math.Min(num2a, 425) - n; // weight of possible values >=n but <425 or <num2a
                    int interval3 = 125 - Math.Min(num1a, 125); // weight of possible values <125 but >=num1a
                    int interval4 = n - Math.Max(num1a, 125); // weight of possible values <n but >=125 or >=num1a

                    scrapTotal = 0;
                    scrapValues[0] = (int)(
                        (
                            (
                                421.44f * interval1 // // average value of this interval * weight ; 421.44f is used because 425 * 0.4 * 0.7 rounds down to 118 (floating point error)
                                + ((float)Math.Min(num2a, 421.44) + n) / 2f * interval2 // average value of this interval * weight
                            ) * 0.7f // reduction
                            + 125f * interval3 // average value of this interval * weight
                            + ((float)Math.Max(num1a, 125) + n - 1f) / 2f * interval4 // average value of this interval * weight (-1 since n is covered by interval2)
                        )
                        * MoonData.scrapValueMultiplier / (num2a - num1a) - 0.0001f); // normalize price and divide by total weight ; account for floating point errors
                    for (int num6 = 0; num6 < scrapAmount; num6++)
                    {
                        scrapValues[num6] = scrapValues[0];
                        scrapTotal += scrapValues[0];
                    }

                }
                else if (num2b < num5) // if scrap can sometimes be below lower limit, average is boosted slightly ; Only happens on experimentation
                {
                    int n = (int)Math.Ceiling((float)num5 / scrapAmount); // minimum scrap value that does not cause boost (Ceiling ensures that the value n will exceed or equal the boost requirement of 1500 or 3000)
                    int interval1 = Math.Max(num2a, 425) - 425; // weight of possible values >=425 but <num2a (since upper bounds are exclusive)
                    int interval2 = Math.Min(num2a, 425) - n; // weight of possible values >=n but <425 or <num2a
                    int interval3 = 125 - Math.Min(num1a, 125); // weight of possible values <125 but >=num1a
                    int interval4 = n - Math.Max(num1a, 125); // weight of possible values <n but >=125 or >=num1a

                    scrapTotal = 0;
                    scrapValues[0] = (int)(
                        (
                            425f * interval1
                            + ((float)Math.Min(num2a, 424) + n) / 2f * interval2
                            + (
                                123.22f * interval3 // 123.22f is used because 125 * 0.4 * 1.4 rounds down to 69 (floating point error)
                                + ((float)Math.Max(num1a, 123.22) + n - 1f) / 2f * interval4
                            ) * 1.4f // apply boost
                        )
                        * MoonData.scrapValueMultiplier / (num2a - num1a) - 0.0001f);
                    for (int num6 = 0; num6 < scrapAmount; num6++)
                    {
                        scrapValues[num6] = scrapValues[0];
                        scrapTotal += scrapValues[0];
                    }
                } // these averages are approximations, not the actual average value 
                  // no scrap sid can both possibly be below lower limit and over the upper limit

            }
        }
        public static void DetermineScrapTypes()
        {
            typeSID = -1;
            cycleCheck = -1; // arbitrary custom value to track cycle errors
            numCycle = 0;

            if (AnomalyRandom.Next(0, 500) <= 20)
            {

                typeSID = AnomalyRandom.Next(0, currentlevel.scrapRarity.Length);
                bool flag = false;
                for (int i = 0; i < 2; i++)
                {
                    if (currentlevel.scrapRarity[typeSID] < 5 || MoonData.twoHandedScrap.IndexOf(currentlevel.scrapType[typeSID]) != -1)
                    {
                        typeSID = AnomalyRandom.Next(0, currentlevel.scrapRarity.Length);
                        continue;
                    }
                    flag = true;
                    break;

                }
                if (!flag && AnomalyRandom.Next(0, 100) < 60)
                {
                    typeSID = -1;
                }
                else
                {
                    scrapToSpawn.Clear();
                    for (int k = 0; k < scrapAmount; k++)
                    {
                        scrapToSpawn.Add(currentlevel.scrapType[typeSID]);

                    }
                }

            }
            if (typeSID == -1)
            {
                scrapToSpawn.Clear();
                for (int k = 0; k < scrapAmount; k++)
                {
                    scrapToSpawn.Add(currentlevel.scrapType[GetRandomWeightedIndex(scrapSum, currentlevel.scrapRarity, AnomalyRandom)]);
                }
                if (cycleCheck == 0) // cycle sizes always at their third item in their loop
                {
                    cycleCheck = scrapAmount - numCycle + 2;
                }
            }

        }
        public static int MeteorShower()
        {
            Random random = new Random(seed + 28);
            if (random.Next(0, 1000) < 7)
            {
                return random.Next(5, 80) * 17 + 700;
            }
            else { return 0; }
        }
        public static void Infestations()
        {
            // this is complicated due to storing date information; the game code simply sets the enemy id as the rush type or sets it to -1 if no rush

            rushType = 0;
            indoorFog = 0;
            System.Random random2 = new System.Random(seed + 5781);
            int eval0 = random2.Next(0, 2750); // 5 5 date -> rush2 && fog2 ; 5 5 !date -> rush1 fog1 ; 15 5 date ->  rush1 fog2 ; 15 5 !date -> rush0 fog1 ; 150 2 1 date -> rush1 fog2 ; 150 2 1 !date -> rush0 fog1 ; 150 5 1 date -> 1 fog3; 150 5 1 !date -> fog2
            int eval1 = random2.Next(0, 1000);
            if (eval0 < 125) // succeeds only when the date is 10/23 ; random2.Next(0, 110) < 5
            {
                rushType = 1;
                if (eval0 < 11)
                {
                    rushType = 2;
                    indoorFog = (eval1 < 200 ? 3 : 0);
                }
                else if (eval1 < 20)
                {
                    indoorFog = 3;
                }
                else if (eval1 < 200)
                {
                    indoorFog = 1;
                }

            }
            else if (eval1 < 4) // only generate a second random2 value when date is true and failed 5% check ; random2.Next(0, 1000) < 4
            {
                rushType = 1;
                indoorFog = (random2.Next(0, 100) < 20 ? 3 : 2); // since eval1 passed 4/1000, the !10/23 check of 3/150 for fog must always occur, hence >=2

            }

            // when first check passes, both 10/23 and !10/23 are in sync (two checks used)
            // when only second check passes, !10/23 must have rushType = 0
            // despite the second check's passing making the nutcracker check not synced with !10/23 (three checks used), !10/23 never sees the nutcracker check

            if (rushType != 0) // old (!date && random2.Next(0, 210) < 4) || random2.Next(0, 1000) < 7) (2.59% || 0.7%) -> (0.4% || 4.93%)
            {


                // rushType meaning: 0 -> never, 1 -> 10/23 nut, 2 -> always nut, 3 -> 10/23 bug, 4 -> always bug

                if (random2.Next(0, 100) < 25 && doNutSpawns) // runs check if not Vow ; normally random2.Next(0, 100) < 25
                {

                    scrapTotal += 600;

                }
                else if (doBugSpawns) // run if not Rend
                {
                    rushType += 2;
                }
                else
                {

                    rushType = 0;
                }
            }
            else // when rushType is 0, !10/23 has used one check and 10/23 has used 2 checks
            {

                indoorFog += (eval1 < 20 ? 2 : 0); // does !10/23 check cause fog? 
                indoorFog += (random2.Next(0, 150) < 3 ? 1 : 0); // does 10/23 check cause fog?


            }
        }
        public static void InitializeRandomNumberGenerators()
        {

            LevelRandom = new System.Random(seed);
            AnomalyRandom = new System.Random(seed + 5);
            DaytimeEnemySpawnRandom = new System.Random(seed + 43);


        }

        public static bool GetRandomNavMeshPositionInBoxPredictable(System.Random randomSeed) // Just simulating this usage for accurate scrap results
        {

            float x = RandomNumberInRadius(randomSeed);
            float y2 = RandomNumberInRadius(randomSeed);
            float z = RandomNumberInRadius(randomSeed);
            return true;


        }
        private static float RandomNumberInRadius(System.Random randomSeed)
        {
            return ((float)randomSeed.NextDouble() - 0.5f);
        }

        public static int GetRandomWeightedIndex(int weightSum, int[] weightRarity, System.Random randomSeed) // in game, weight sum is calculated every time; weightSum just saves computing time
        {
            if (cycleCheck == 0)
            {
                numCycle++; // start counting cycle loop ; only designed to measure scrap determinations
            }
            float num2 = (float)randomSeed.NextDouble();
            float num3 = 0f;
            for (int i = 0; i < weightRarity.Length; i++)
            {

                num3 += (float)weightRarity[i] / (float)weightSum; 
                if (num3 >= num2)
                {
                    return i;
                }

            }

            // floating point error backup system

            if (weightSum != scrapSum)
            {
                Console.WriteLine($"!!! Seed {seed} triggered a non-scrap cycle!!!"); // only scrap cycles should be possible; just a failsafe in case other weight systems fail
            }
            else if (cycleCheck == -1) // marks end of first loop
            {
                cycleCheck = 0;
            }
            else if (cycleCheck == 0) // on second loop, set loop size and stop counting
            {
                cycleCheck = numCycle;
            }
            InitializeRandomNumberGenerators();

            return randomSeed.Next(0, weightRarity.Length);

        }


        public static void Searcher(int seedsToSearch)
        {
            using FileStream fs = new FileStream($"{currentlevel.moonName}.bin.zst", FileMode.Create);
            using CompressionStream zstd = new(fs);
            using BinaryWriter bw = new BinaryWriter(zstd);


            int f = seedsToSearch / 100;
            int k = 1;

            for (seed = 1; seed <= seedsToSearch; seed++)
            {
                if (seed == f)
                {
                    Console.WriteLine($"{k++}%");
                    f += seedsToSearch / 100;
                }

                InitializeRandomNumberGenerators();

                dunType = GetRandomWeightedIndex(currentlevel.dunRarity.Sum(), currentlevel.dunRarity, LevelRandom);
                if (currentlevel.flipDunTypes) // rend and dine have their dun rarities flipped from normal
                {
                    dunType = (dunType == 0 ? 1 : dunType == 1 ? 0 : dunType);
                }

                scrapAmount = AnomalyRandom.Next(currentlevel.numMin, (currentlevel.numMax + 1));

                if (dunType == 2) // mineshaft bonus
                {
                    scrapAmount += 6;
                }

                DetermineScrapTypes();

                EvaluateScrapValues();

                Infestations();

                int msTime = MeteorShower();

                currentDaytimeEnemyPower = 0;
                enemyWave = 1; // placeholder for simulating multiple waves ; only the first wave is used
                SpawnDaytimeEnemies();

                if (dunType == 0) // facility apparatus bonus (calculated only after scrap type determination due to scrap amount increasing)
                {
                    scrapTotal += 80;
                    scrapAmount++;
                }

                if (cycleCheck == -1) // normalize value for file storage
                {
                    cycleCheck = 0;
                }

                if (typeSID == -1) // normalize value for file storage
                {
                    typeSID = scrapLength;
                }

                // start writing data
                // standard data
                ulong seedMixA = 0;

                seedMixA |= (ulong)seed << 0;
                seedMixA |= (ulong)dunType << 27;
                seedMixA |= (ulong)rushType << 29;
                seedMixA |= (ulong)indoorFog << 32;
                seedMixA |= (ulong)msTime << 34;
                seedMixA |= (ulong)typeSID << 46;
                seedMixA |= (ulong)scrapAmount << 52;
                seedMixA |= (ulong)cycleCheck << 58;
                
                bw.Write(seedMixA);

                // scrap mixes
                ulong seedMixC = 0;
                ulong seedMixD = 0;
                ulong seedMixE = 0;
                ulong seedMixF = 0;

                List<ulong> seedMixes = 
                [
                    seedMixC, seedMixD, seedMixE, seedMixF
                ];

                if (typeSID == scrapLength) // if SID, do not count scrap ; this is to save space
                {
                    int fileLocation = 0;
                    int fileLimit = scrapFileSizes[0];
                    int bitLocation = 0;
                    for (int i = 0; i < scrapLength; i++)
                    {
                        int scrapToStore = scrapToSpawn.Count(x => x == currentlevel.scrapType[i]);
                        if (scrapToStore >= Math.Pow(2, countBits))
                        {
                            if (cycleCheck == 0)
                            {
                                Console.WriteLine($"ERROR: Seed {seed} has {scrapToStore} {currentlevel.scrapType[i]}! Skipping Moon...");
                                seed = 100000000;
                                break;
                            }

                            Console.WriteLine($"{seed} was a cycle that chose {currentlevel.scrapType[i]} twice!");
                            scrapToStore = (int)Math.Pow(2, countBits) - 1; 
                            // if the seed is a cycle, the count is capped; This happens only when a cycle chooses the same item twice (1/2+ of total), and should only happen to one of its scrap types.
                            // since only one type should be affected, its count can be determined from the total count
                        }


                        if (bitLocation + countBits > fileLimit)
                        {
                            bitLocation -= fileLimit;
                            fileLocation++;
                            fileLimit = scrapFileSizes[fileLocation];
                        }

                        seedMixes[fileLocation] |= (ulong)scrapToStore << bitLocation;
                        bitLocation += countBits;
                    }
                }

                for (int j = 0; j < scrapFileSizes.Count(); j++)
                {
                    if (scrapFileSizes[j] > 32)
                    {
                        bw.Write(seedMixes[j]);
                    }
                    else if (scrapFileSizes[j] > 16)
                    {
                        bw.Write((uint)seedMixes[j]);
                    }
                    else
                    {
                        bw.Write((ushort)seedMixes[j]);
                    }
                    
                }

                // total mix
                ushort seedMixB = 0;

                seedMixB |= (ushort)scrapTotal;

                bw.Write(seedMixB);

                // bee mix
                if (doDayScrap)
                {
                    ushort seedMixBee = 0;

                    seedMixBee |= (ushort)((ulong)daytimeSpawned[0] << 0);
                    seedMixBee |= (ushort)((ulong)(daytimeSpawned[4] * 3) << 3);
                    seedMixBee |= (ushort)((ulong)dayPotential << 5);

                    bw.Write(seedMixBee);
                }

            }

            Console.WriteLine($"Binary Files Completed for Moon: {currentlevel.moonName}.");
            Console.WriteLine("-----------------------------------------------------------");
        }

    }

    enum Scrap // just for ease of manual interpretation of scrap type (index) into scrap name
    {
        airhorn, bell, bigBolt, bone, bottles, brush, candy, cashregister,
        chemicalJug, clock, clownHorn, comedy, controlPad, cookieMold, dustPan,
        ear, easterEgg, eggBeater, fancyLamp, flask, foot, garbageLid, gift,
        goldBar, goldenCup, hairDryer, hand, heart, homemadeFlashbang, jarOfPickles,
        knee, largeAxle, laserPointer, magic7Ball, magnifyingGlass, metalSheet,
        mug, oldPhone, painting, perfumeBottle, pillBottle, plasticCup, plasticFish,
        redSoda, remote, ribcage, ring, rubberDucky, soccerBall, steeringWheel,
        stopSign, teaKettle, teeth, toiletPaper, tongue, toothpaste, toyCube,
        toyRobot, toyTrain, tragedy, vTypeEngine, whoopieCushion, yieldSign, zedDog

    }

    enum IndoorEnemies
    {
        barber, bracken, spider, butler, coil, girl, hoardingBug, blob, jester, maneater, masked, nutcracker, centipede, puffer, crawler
    }
    enum OutdoorEnemies
    {
        baboonHawk, worm, dog, giant, radmech
    }

    enum DaytimeEnemies
    {
        bee, manti, locust, tuilip, kiwi
    }

    public interface IMoon
    {
        string moonName { get; }
        int[] dunRarity { get; }
        int[] scrapType { get; }
        int[] scrapRarity { get; }
        int numMin { get; }
        int numMax { get; }
        int scrapAveGift { get; }
        bool flipDunTypes { get; }


        int spawnProbabilityRange { get; }
        int daytimeEnemiesProbabilityRange { get; }
        int indoorPower { get; }
        int outdoorPower { get; }
        int daytimePower { get; }
        int[] indoorEnemiesType { get; }
        int[] indoorEnemiesRarity { get; }
        int[] outdoorEnemiesType { get; }
        int[] outdoorEnemiesRarity { get; }
        int[] daytimeEnemiesType { get; }
        int[] daytimeEnemiesRarity { get; }
        float[] enemySpawnChanceThroughoutDay { get; }
        float[] daytimeEnemySpawnChanceThroughDay { get; }
    }

    class MoonData
    {
        public static string[] dungeon = ["Facility", "Mansion", "Mineshaft"];
        public static string[] scrap = ["airhorn", "bell", "bigBolt", "bone", "bottles", "brush", "candy", "cashRegister", "chemicalJug", "clock", "clownHorn", "comedy", "controlPad", "cookieMoldPan", "dustPan", "ear", "easterEgg", "eggBeater", "fancyLamp", "flask", "foot", "garbageLid", "gift", "goldBar", "goldenCup", "hairDryer", "hand", "heart", "homemadeFlashbang", "jarOfPickles", "knee", "largeAxle", "laserPointer", "magic7Ball", "magnifyingGlass", "metalSheet", "mug", "oldPhone", "painting", "perfumeBottle", "pillBottle", "plasticCup", "plasticFish", "redSoda", "remote", "ribcage", "ring", "rubberDucky", "soccerBall", "steeringWheel", "stopSign", "teaKettle", "teeth", "toiletPaper", "tongue", "toothpaste", "toyCube", "toyRobot", "toyTrain", "tragedy", "vTypeEngine", "whoopieCushion", "yieldSign", "zedDog"];
        public static int[] scrapMinValue = [130, 120, 50, 17, 110, 20, 15, 200, 80, 110, 130, 70, 85, 30, 30, 7, 55, 30, 150, 40, 15, 50, 30, 255, 100, 150, 10, 60, 25, 80, 20, 90, 80, 90, 110, 25, 60, 120, 150, 120, 40, 30, 70, 45, 50, 20, 130, 5, 110, 40, 50, 80, 150, 150, 8, 35, 60, 140, 130, 70, 50, 15, 45, 1];
        public static int[] scrapMaxValue = [179, 199, 79, 36, 139, 89, 89, 399, 209, 139, 179, 129, 159, 99, 79, 34, 129, 109, 319, 109, 54, 109, 69, 524, 199, 249, 29, 249, 69, 149, 44, 139, 249, 179, 149, 54, 169, 159, 309, 259, 99, 89, 99, 224, 119, 64, 199, 249, 179, 79, 129, 139, 209, 219, 39, 119, 109, 219, 209, 129, 139, 89, 89, 499];
        public static int[] scrapAveValue = [62, 64, 26, 11, 50, 22, 21, 120, 58, 50, 62, 40, 49, 26, 22, 8, 37, 28, 94, 30, 14, 32, 20, 156, 60, 80, 8, 62, 19, 46, 13, 46, 66, 54, 52, 16, 46, 56, 92, 76, 28, 24, 34, 54, 34, 17, 66, 51, 58, 24, 36, 44, 72, 74, 9, 31, 34, 72, 68, 40, 38, 21, 27, 100];
        public static int[] scrapAveSIDValue = [62, 64, 50, 50, 51, 50, 50, 120, 61, 51, 62, 50, 53, 50, 50, 50, 50, 50, 94, 50, 50, 50, 50, 148, 61, 80, 50, 66, 50, 52, 50, 51, 68, 57, 53, 50, 54, 56, 92, 76, 50, 50, 50, 61, 50, 50, 66, 63, 58, 50, 50, 51, 72, 74, 50, 50, 50, 72, 68, 50, 50, 50, 50, 104];
        // v81 notes: 

        public static float scrapValueMultiplier = 0.4f;
        public static int[] twoHandedScrap = [4, 7, 8, 12, 18, 21, 27, 31, 38, 45, 48, 53, 60];

        public static string[] indoorEnemies = ["Barber", "Bracken", "Bunker Spider", "Butler", "Coil-Head", "Ghost Girl", "Hoarding Bug", "Hygrodere", "Jester", "Maneater", "Masked", "Nutcracker", "Snare Flee", "Spore Lizard", "Thumper"];
        public static int[] indoorEnemiesPower = [1, 3, 2, 2, 1, 2, 1, 1, 3, 2, 1, 1, 1, 1, 3];
        public static int[] indoorEnemiesMaxSpawn = [1, 1, 1, 7, 5, 1, 8, 2, 1, 1, 10, 10, 4, 2, 4];
        public static string[] outdoorEnemies = ["Baboon Hawk", "Earth Leviathan", "Eyeless Dog", "Forest Keeper", "Old Bird"];
        public static int[] outdoorEnemiesPower = [1, 2, 2, 3, 3];
        public static int[] outdoorEnemiesMaxSpawn = [15, 3, 8, 3, 20];
        public static string[] daytimeEnemies = ["Circuit Bee", "Manticoil", "Roaming Locust", "Tuilip Snake", "Giant Sapsucker"];
        public static float[] daytimeEnemiesPower = [1f, 1f, 1f, 0.5f, 4f]; // V73 [1, 1, 1, 2, 2]
        public static int[] daytimeEnemiesMaxSpawn = [6, 16, 5, 12, 1];

        public static float[] beeCurve = [0.9651623f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f];
        public static float[] mantiCurve = [1.001262f, 1.002524f, 1.003787f, 1.005049f, 0.9329059f, 0.8394113f, 0.6857241f, 0.4409307f, 0f];
        public static float[] locustCurve = [0.6463307f, 0.7776324f, 0.8984846f, 0.9850591f, 1.004226f, 0.8372099f, 0.5404056f, 0.2244551f, 0f];
        public static float[] tuilipCurve = [0.6489306f, 0.7966325f, 0.8722569f, 0.9173182f, 0.9509419f, 0.9745752f, 0.9896646f, 0.9976573f, 1f];
        public static float[] kiwiCurve = [0.9651623f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f];
        public static float[][] daytimeEnemyCurve = [beeCurve, mantiCurve, locustCurve, tuilipCurve, kiwiCurve];

    }
    class Experimentation : IMoon
    {
        public string moonName { get; } = "Experimentation";
        public int[] dunRarity { get; } = [300, 1, 3];
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.metalSheet, (int)Scrap.laserPointer, (int)Scrap.bigBolt, (int)Scrap.bottles, (int)Scrap.ring, (int)Scrap.steeringWheel, (int)Scrap.cookieMold, (int)Scrap.eggBeater, (int)Scrap.jarOfPickles, (int)Scrap.dustPan, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.cashregister, (int)Scrap.candy, (int)Scrap.goldBar, (int)Scrap.yieldSign, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.flask, (int)Scrap.easterEgg];
        public int[] scrapRarity { get; } = [80, 90, 12, 88, 4, 80, 19, 3, 32, 5, 10, 10, 32, 3, 3, 3, 2, 1, 6, 22, 17, 42, 5];
        public int numMin { get; } = 8;
        public int numMax { get; } = 11;
        public int scrapAveGift { get; } = 44;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 4;
        public int daytimeEnemiesProbabilityRange { get; } = 5;
        public int indoorPower { get; } = 4;
        public int outdoorPower { get; } = 8;
        public int daytimePower { get; } = 5;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.centipede, (int)IndoorEnemies.spider, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.bracken, (int)IndoorEnemies.crawler, (int)IndoorEnemies.blob, (int)IndoorEnemies.girl, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker];
        public int[] indoorEnemiesRarity { get; } = [51, 58, 28, 13, 16, 31, 1, 28, 1];
        public int[] outdoorEnemiesType { get; } = [2, 3, 1, 4];
        public int[] outdoorEnemiesRarity { get; } = [75, 1, 56, 3];
        public int[] daytimeEnemiesType { get; } = [0, 1, 2];
        public int[] daytimeEnemiesRarity { get; } = [22, 74, 52];
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [1.45262f, 0.7228957f, 0.1680334f, -0.6017671f, -3.347293f, -6.742905f, -10.13549f, -13.37474f, -14.8181f];
    }
    class Assurance : IMoon
    {
        public string moonName { get; } = "Assurance";
        public int[] dunRarity { get; } = [300, 3, 40];
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.metalSheet, (int)Scrap.bigBolt, (int)Scrap.toyCube, (int)Scrap.laserPointer, (int)Scrap.bottles, (int)Scrap.remote, (int)Scrap.cookieMold, (int)Scrap.toyRobot, (int)Scrap.magnifyingGlass, (int)Scrap.stopSign, (int)Scrap.teaKettle, (int)Scrap.jarOfPickles, (int)Scrap.mug, (int)Scrap.redSoda, (int)Scrap.steeringWheel, (int)Scrap.oldPhone, (int)Scrap.hairDryer, (int)Scrap.eggBeater, (int)Scrap.brush, (int)Scrap.bottles, (int)Scrap.bell, (int)Scrap.clownHorn, (int)Scrap.airhorn, (int)Scrap.cashregister, (int)Scrap.yieldSign, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.flask, (int)Scrap.whoopieCushion, (int)Scrap.comedy, (int)Scrap.tragedy, (int)Scrap.easterEgg, (int)Scrap.toiletPaper, (int)Scrap.controlPad, (int)Scrap.plasticCup, (int)Scrap.garbageLid, (int)Scrap.soccerBall, (int)Scrap.zedDog];
        public int[] scrapRarity { get; } = [30, 40, 10, 23, 59, 31, 8, 58, 13, 49, 6, 17, 34, 32, 20, 12, 12, 19, 7, 4, 34, 26, 53, 16, 10, 10, 3, 17, 14, 13, 12, 11, 5, 3, 32, 19, 15, 19, 31, 23, 1];
        public int numMin { get; } = 13; // ^ Bottles appear twice with 58 and 53 rarity for a total expected of 111
        public int numMax { get; } = 15;
        public int scrapAveGift { get; } = 51;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 8;
        public int daytimeEnemiesProbabilityRange { get; } = 2;
        public int indoorPower { get; } = 6;
        public int outdoorPower { get; } = 8;
        public int daytimePower { get; } = 7;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.centipede, (int)IndoorEnemies.spider, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.bracken, (int)IndoorEnemies.crawler, (int)IndoorEnemies.blob, (int)IndoorEnemies.girl, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.barber, (int)IndoorEnemies.maneater];
        public int[] indoorEnemiesRarity { get; } = [93, 69, 78, 14, 24, 28, 1, 14, 1, 3, 34];
        public int[] outdoorEnemiesType { get; } = [2, 3, 1, 0];
        public int[] outdoorEnemiesRarity { get; } = [78, 1, 95, 20];
        public int[] daytimeEnemiesType { get; } = [1, 2, 0, 3, 4];
        public int[] daytimeEnemiesRarity { get; } = [100, 46, 43, 3, 8]; // V73 [100, 46, 43, 3, 7]
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [11.27784f, 10.93607f, 5.692048f, -0.8214536f, -4.971664f, -7.396788f, -10.68405f, -13.57726f, -14.8181f];
    }

    class Vow : IMoon
    {
        public string moonName { get; } = "Vow";
        public int[] dunRarity { get; } = [300, 3, 192]; // V73: [300, 3, 250]
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.metalSheet, (int)Scrap.bigBolt, (int)Scrap.toyCube, (int)Scrap.laserPointer, (int)Scrap.bottles, (int)Scrap.remote, (int)Scrap.cookieMold, (int)Scrap.toyRobot, (int)Scrap.magnifyingGlass, (int)Scrap.stopSign, (int)Scrap.teaKettle, (int)Scrap.jarOfPickles, (int)Scrap.mug, (int)Scrap.redSoda, (int)Scrap.steeringWheel, (int)Scrap.oldPhone, (int)Scrap.rubberDucky, (int)Scrap.eggBeater, (int)Scrap.brush, (int)Scrap.bottles, (int)Scrap.bell, (int)Scrap.chemicalJug, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.cashregister, (int)Scrap.yieldSign, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.flask, (int)Scrap.whoopieCushion, (int)Scrap.easterEgg, (int)Scrap.toiletPaper, (int)Scrap.garbageLid, (int)Scrap.controlPad, (int)Scrap.plasticCup, (int)Scrap.soccerBall, (int)Scrap.zedDog];
        public int[] scrapRarity { get; } = [25, 25, 30, 16, 31, 17, 8, 30, 19, 49, 6, 21, 40, 40, 27, 18, 12, 17, 7, 24, 56, 46, 24, 33, 46, 10, 35, 5, 16, 14, 12, 30, 38, 28, 34, 27, 12, 22, 22, 1];
        public int numMin { get; } = 12; // ^ Bottles appear twice with 30 and 24 rarity for a total expected of 54
        public int numMax { get; } = 14;
        public int scrapAveGift { get; } = 53;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 3;
        public int daytimeEnemiesProbabilityRange { get; } = 7;
        public int indoorPower { get; } = 7;
        public int outdoorPower { get; } = 6;
        public int daytimePower { get; } = 17;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.centipede, (int)IndoorEnemies.spider, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.bracken, (int)IndoorEnemies.crawler, (int)IndoorEnemies.blob, (int)IndoorEnemies.coil, (int)IndoorEnemies.puffer, (int)IndoorEnemies.barber, (int)IndoorEnemies.maneater];
        public int[] indoorEnemiesRarity { get; } = [48, 40, 63, 80, 9, 28, 6, 19, 14, 53];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.giant, (int)OutdoorEnemies.dog, (int)OutdoorEnemies.worm, (int)OutdoorEnemies.baboonHawk];
        public int[] outdoorEnemiesRarity { get; } = [100, 4, 18, 31];
        public int[] daytimeEnemiesType { get; } = [(int)DaytimeEnemies.manti, (int)DaytimeEnemies.bee, (int)DaytimeEnemies.locust, (int)DaytimeEnemies.tuilip, (int)DaytimeEnemies.kiwi];
        public int[] daytimeEnemiesRarity { get; } = [100, 36, 40, 3, 14]; // V73 [100, 36, 40, 4, 79]
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [3.274095f, 3.642683f, 2.448636f, 0.3404133f, -2.43269f, -6.05351f, -10.12535f, -13.4473f, -14.8181f];
    }

    class Offense : IMoon
    {
        public string moonName { get; } = "Offense";
        public int[] dunRarity { get; } = [64, 4, 300]; // v73: [300, 4, 200]
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.metalSheet, (int)Scrap.bigBolt, (int)Scrap.toyCube, (int)Scrap.laserPointer, (int)Scrap.bottles, (int)Scrap.remote, (int)Scrap.cookieMold, (int)Scrap.toyRobot, (int)Scrap.magnifyingGlass, (int)Scrap.stopSign, (int)Scrap.teaKettle, (int)Scrap.teeth, (int)Scrap.oldPhone, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.yieldSign, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.easterEgg, (int)Scrap.comedy, (int)Scrap.flask, (int)Scrap.controlPad, (int)Scrap.toiletPaper, (int)Scrap.plasticCup, (int)Scrap.garbageLid, (int)Scrap.clock, (int)Scrap.toyTrain, (int)Scrap.zedDog];
        public int[] scrapRarity { get; } = [94, 80, 28, 65, 89, 18, 10, 46, 15, 6, 9, 20, 27, 24, 11, 8, 15, 18, 28, 13, 19, 19, 10, 40, 40, 18, 19, 31, 6, 6, 1];
        public int numMin { get; } = 14;
        public int numMax { get; } = 18; //v81: 14-17
        public int scrapAveGift { get; } = 51;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 4;
        public int daytimeEnemiesProbabilityRange { get; } = 10;
        public int indoorPower { get; } = 12;
        public int outdoorPower { get; } = 8;
        public int daytimePower { get; } = 20;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.centipede, (int)IndoorEnemies.spider, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.bracken, (int)IndoorEnemies.crawler, (int)IndoorEnemies.blob, (int)IndoorEnemies.coil, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.maneater];
        public int[] indoorEnemiesRarity { get; } = [27, 44, 16, 3, 55, 32, 25, 7, 2, 17];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.dog, (int)OutdoorEnemies.giant, (int)OutdoorEnemies.worm, (int)OutdoorEnemies.baboonHawk, (int)OutdoorEnemies.radmech];
        public int[] outdoorEnemiesRarity { get; } = [100, 9, 37, 60, 5];
        public int[] daytimeEnemiesType { get; } = [(int)DaytimeEnemies.manti];
        public int[] daytimeEnemiesRarity { get; } = [100];
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [-8.873613f, -7.637432f, -6.569365f, -7.21982f, -9.924208f, -12.11207f, -13.63652f, -14.5283f, -14.8181f];
    }

    class March : IMoon
    {
        public string moonName { get; } = "March";
        public int[] dunRarity { get; } = [300, 0, 0];
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.metalSheet, (int)Scrap.bigBolt, (int)Scrap.toyCube, (int)Scrap.laserPointer, (int)Scrap.bottles, (int)Scrap.remote, (int)Scrap.cookieMold, (int)Scrap.toyRobot, (int)Scrap.magnifyingGlass, (int)Scrap.stopSign, (int)Scrap.teaKettle, (int)Scrap.goldBar, (int)Scrap.cashregister, (int)Scrap.clownHorn, (int)Scrap.airhorn, (int)Scrap.candy, (int)Scrap.redSoda, (int)Scrap.yieldSign, (int)Scrap.gift, (int)Scrap.flask, (int)Scrap.easterEgg, (int)Scrap.garbageLid, (int)Scrap.controlPad, (int)Scrap.plasticCup, (int)Scrap.toiletPaper, (int)Scrap.soccerBall, (int)Scrap.zedDog];
        public int[] scrapRarity { get; } = [77, 72, 28, 83, 89, 18, 3, 67, 16, 24, 2, 4, 29, 24, 4, 3, 43, 34, 3, 2, 8, 24, 42, 65, 20, 11, 27, 32, 21, 1];
        public int numMin { get; } = 13;
        public int numMax { get; } = 16;
        public int scrapAveGift { get; } = 52;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 4;
        public int daytimeEnemiesProbabilityRange { get; } = 7;
        public int indoorPower { get; } = 14;
        public int outdoorPower { get; } = 12;
        public int daytimePower { get; } = 14; // V73 20
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.centipede, (int)IndoorEnemies.spider, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.bracken, (int)IndoorEnemies.crawler, (int)IndoorEnemies.blob, (int)IndoorEnemies.coil, (int)IndoorEnemies.puffer, (int)IndoorEnemies.jester, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.maneater];
        public int[] indoorEnemiesRarity { get; } = [38, 64, 36, 56, 74, 15, 10, 9, 1, 3, 9];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.giant, (int)OutdoorEnemies.dog, (int)OutdoorEnemies.worm, (int)OutdoorEnemies.baboonHawk];
        public int[] outdoorEnemiesRarity { get; } = [64, 38, 16, 85];
        public int[] daytimeEnemiesType { get; } = [(int)DaytimeEnemies.manti, (int)DaytimeEnemies.locust, (int)DaytimeEnemies.bee, (int)DaytimeEnemies.tuilip];
        public int[] daytimeEnemiesRarity { get; } = [83, 39, 70, 1]; // V73 [83, 39, 70, 4]
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [4.995485f, 4.879643f, 3.308665f, 0.6385722f, -2.432501f, -6.05351f, -10.12535f, -13.4473f, -14.8181f];
    }

    class Adamance : IMoon
    {
        public string moonName { get; } = "Adamance";
        public int[] dunRarity { get; } = [300, 8, 48]; // [300, 13, 135]
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.metalSheet, (int)Scrap.bigBolt, (int)Scrap.toyCube, (int)Scrap.laserPointer, (int)Scrap.bottles, (int)Scrap.remote, (int)Scrap.cookieMold, (int)Scrap.toyRobot, (int)Scrap.magnifyingGlass, (int)Scrap.stopSign, (int)Scrap.teaKettle, (int)Scrap.jarOfPickles, (int)Scrap.mug, (int)Scrap.redSoda, (int)Scrap.steeringWheel, (int)Scrap.oldPhone, (int)Scrap.rubberDucky, (int)Scrap.eggBeater, (int)Scrap.brush, (int)Scrap.bottles, (int)Scrap.bell, (int)Scrap.chemicalJug, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.cashregister, (int)Scrap.yieldSign, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.flask, (int)Scrap.whoopieCushion, (int)Scrap.easterEgg, (int)Scrap.toiletPaper, (int)Scrap.plasticCup, (int)Scrap.garbageLid, (int)Scrap.controlPad];
        public int[] scrapRarity { get; } = [30, 40, 32, 16, 31, 17, 8, 44, 24, 40, 9, 21, 40, 32, 20, 12, 12, 12, 4, 25, 50, 46, 24, 37, 41, 13, 9, 7, 16, 21, 23, 30, 20, 50, 40, 16, 19, 16];
        public int numMin { get; } = 14; // ^ Bottles appear twice with 44 and 24 rarity for a total expected of 68
        public int numMax { get; } = 16; //v81: 16-18
        public int scrapAveGift { get; } = 53;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 2;
        public int daytimeEnemiesProbabilityRange { get; } = 8;
        public int indoorPower { get; } = 13;
        public int outdoorPower { get; } = 11; // V73 13
        public int daytimePower { get; } = 20;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.girl, (int)IndoorEnemies.spider, (int)IndoorEnemies.centipede, (int)IndoorEnemies.blob, (int)IndoorEnemies.bracken, (int)IndoorEnemies.coil, (int)IndoorEnemies.crawler, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.jester, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.masked, (int)IndoorEnemies.butler, (int)IndoorEnemies.barber, (int)IndoorEnemies.maneater];
        public int[] indoorEnemiesRarity { get; } = [2, 62, 42, 17, 40, 10, 67, 57, 7, 33, 8, 5, 10, 3, 43];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.giant, (int)OutdoorEnemies.dog, (int)OutdoorEnemies.worm, (int)OutdoorEnemies.radmech, (int)OutdoorEnemies.baboonHawk];
        public int[] outdoorEnemiesRarity { get; } = [16, 19, 6, 2, 52];
        public int[] daytimeEnemiesType { get; } = [(int)DaytimeEnemies.manti, (int)DaytimeEnemies.locust, (int)DaytimeEnemies.bee, (int)DaytimeEnemies.tuilip, (int)DaytimeEnemies.kiwi];
        public int[] daytimeEnemiesRarity { get; } = [63, 30, 12, 7, 4]; // V73 [63, 30, 17, 9, 5]
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [5.175917f, 4.57081f, 3.71383f, 2.728955f, 1.740165f, 0.8714395f, 0.2467585f, -0.01302307f, 0.03682685f];
    }

    class Rend : IMoon
    {
        public string moonName { get; } = "Rend";
        public int[] dunRarity { get; } = [300, 5, 28]; // mansion, facility, mineshaft
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.bigBolt, (int)Scrap.fancyLamp, (int)Scrap.toyCube, (int)Scrap.jarOfPickles, (int)Scrap.laserPointer, (int)Scrap.goldenCup, (int)Scrap.painting, (int)Scrap.bell, (int)Scrap.ring, (int)Scrap.toyRobot, (int)Scrap.toothpaste, (int)Scrap.brush, (int)Scrap.pillBottle, (int)Scrap.perfumeBottle, (int)Scrap.mug, (int)Scrap.bottles, (int)Scrap.magnifyingGlass, (int)Scrap.hairDryer, (int)Scrap.oldPhone, (int)Scrap.redSoda, (int)Scrap.teeth, (int)Scrap.magic7Ball, (int)Scrap.rubberDucky, (int)Scrap.teaKettle, (int)Scrap.cashregister, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.candy, (int)Scrap.gift, (int)Scrap.comedy, (int)Scrap.tragedy, (int)Scrap.easterEgg, (int)Scrap.controlPad, (int)Scrap.garbageLid, (int)Scrap.toiletPaper, (int)Scrap.plasticCup, (int)Scrap.soccerBall, (int)Scrap.toyTrain, (int)Scrap.clock, (int)Scrap.zedDog, (int)Scrap.foot]; // v81 added foot
        public int[] scrapRarity { get; } = [2, 2, 16, 4, 49, 33, 4, 5, 24, 56, 48, 19, 46, 24, 25, 4, 28, 44, 46, 35, 43, 17, 26, 31, 23, 16, 25, 16, 8, 8, 14, 19, 53, 25, 57, 33, 7, 13, 29, 28, 35, 35, 2, 2]; //v81 added 2 for foot
        public int numMin { get; } = 18;
        public int numMax { get; } = 25;
        public int scrapAveGift { get; } = 69;
        public bool flipDunTypes { get; } = true;


        public int spawnProbabilityRange { get; } = 3;
        public int daytimeEnemiesProbabilityRange { get; } = 10;
        public int indoorPower { get; } = 10;
        public int outdoorPower { get; } = 6;
        public int daytimePower { get; } = 20;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.girl, (int)IndoorEnemies.spider, (int)IndoorEnemies.centipede, (int)IndoorEnemies.blob, (int)IndoorEnemies.bracken, (int)IndoorEnemies.coil, (int)IndoorEnemies.jester, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.masked, (int)IndoorEnemies.butler, (int)IndoorEnemies.barber];
        public int[] indoorEnemiesRarity { get; } = [20, 43, 31, 6, 51, 43, 60, 7, 100, 25, 18, 11];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.giant, (int)OutdoorEnemies.dog];
        public int[] outdoorEnemiesRarity { get; } = [60, 74];
        public int[] daytimeEnemiesType { get; } = [];
        public int[] daytimeEnemiesRarity { get; } = [];
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f];
    }

    class Dine : IMoon
    {
        public string moonName { get; } = "Dine";
        public int[] dunRarity { get; } = [300, 7, 17]; // mansion, facility, mineshaft // v73: [300, 7, 140]
        public int[] scrapType { get; } = [(int)Scrap.whoopieCushion, (int)Scrap.easterEgg, (int)Scrap.hand, (int)Scrap.bone, (int)Scrap.ribcage, (int)Scrap.ear, (int)Scrap.foot, (int)Scrap.knee, (int)Scrap.heart, (int)Scrap.tongue];
        public int[] scrapRarity { get; } = [1, 1, 100, 79, 79, 41, 100, 55, 6, 32];
        public int numMin { get; } = 200;
        public int numMax { get; } = 249;
        public int scrapAveGift { get; } = 24;
        public bool flipDunTypes { get; } = true;


        public int spawnProbabilityRange { get; } = 4;
        public int daytimeEnemiesProbabilityRange { get; } = 0;
        public int indoorPower { get; } = 10; // V73 16
        public int outdoorPower { get; } = 9; // V73 7
        public int daytimePower { get; } = 20;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.girl, (int)IndoorEnemies.spider, (int)IndoorEnemies.centipede, (int)IndoorEnemies.blob, (int)IndoorEnemies.bracken, (int)IndoorEnemies.coil, (int)IndoorEnemies.crawler, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.jester, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.butler, (int)IndoorEnemies.barber, (int)IndoorEnemies.maneater];
        public int[] indoorEnemiesRarity { get; } = [3, 4, 4, 2, 5, 5, 3, 7, 6, 3, 6, 24, 12, 2];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.giant, (int)OutdoorEnemies.dog, (int)OutdoorEnemies.radmech, (int)OutdoorEnemies.worm];
        public int[] outdoorEnemiesRarity { get; } = [100, 50, 13, 3];
        public int[] daytimeEnemiesType { get; } = [];
        public int[] daytimeEnemiesRarity { get; } = [];
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [3.666667f, 3.333333f, 3f, 2.666667f, 2.333333f, 2, 1.666667f, 1.333333f, 1f];
    }

    class Titan : IMoon
    {
        public string moonName { get; } = "Titan";
        public int[] dunRarity { get; } = [300, 57, 115];
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.bigBolt, (int)Scrap.fancyLamp, (int)Scrap.toyCube, (int)Scrap.jarOfPickles, (int)Scrap.laserPointer, (int)Scrap.goldenCup, (int)Scrap.painting, (int)Scrap.bell, (int)Scrap.ring, (int)Scrap.toyRobot, (int)Scrap.toothpaste, (int)Scrap.brush, (int)Scrap.pillBottle, (int)Scrap.perfumeBottle, (int)Scrap.mug, (int)Scrap.bottles, (int)Scrap.magnifyingGlass, (int)Scrap.hairDryer, (int)Scrap.oldPhone, (int)Scrap.redSoda, (int)Scrap.teeth, (int)Scrap.magic7Ball, (int)Scrap.rubberDucky, (int)Scrap.teaKettle, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.tragedy, (int)Scrap.comedy, (int)Scrap.whoopieCushion, (int)Scrap.plasticCup, (int)Scrap.toiletPaper];
        public int[] scrapRarity { get; } = [37, 40, 16, 47, 27, 20, 8, 10, 27, 34, 37, 27, 35, 36, 31, 15, 14, 21, 33, 25, 30, 22, 38, 21, 16, 24, 30, 24, 16, 9, 17, 46, 43, 26, 19, 8];
        public int numMin { get; } = 28;
        public int numMax { get; } = 31;
        public int scrapAveGift { get; } = 63;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 2;
        public int daytimeEnemiesProbabilityRange { get; } = 10;
        public int indoorPower { get; } = 18;
        public int outdoorPower { get; } = 7;
        public int daytimePower { get; } = 20;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.girl, (int)IndoorEnemies.spider, (int)IndoorEnemies.centipede, (int)IndoorEnemies.blob, (int)IndoorEnemies.bracken, (int)IndoorEnemies.coil, (int)IndoorEnemies.crawler, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.jester, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.masked];
        public int[] indoorEnemiesRarity { get; } = [17, 59, 54, 20, 62, 59, 54, 38, 71, 16, 71, 32];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.giant, (int)OutdoorEnemies.dog, (int)OutdoorEnemies.worm, (int)OutdoorEnemies.radmech];
        public int[] outdoorEnemiesRarity { get; } = [32, 80, 4, 7];
        public int[] daytimeEnemiesType { get; } = [];
        public int[] daytimeEnemiesRarity { get; } = [];
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f];
    }

    class Embrion : IMoon
    {
        public string moonName { get; } = "Embrion";
        public int[] dunRarity { get; } = [300, 10, 44];
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.metalSheet, (int)Scrap.bigBolt, (int)Scrap.toyCube, (int)Scrap.laserPointer, (int)Scrap.bottles, (int)Scrap.remote, (int)Scrap.cookieMold, (int)Scrap.toyRobot, (int)Scrap.magnifyingGlass, (int)Scrap.stopSign, (int)Scrap.teaKettle, (int)Scrap.teeth, (int)Scrap.oldPhone, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.yieldSign, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.easterEgg, (int)Scrap.controlPad, (int)Scrap.toiletPaper, (int)Scrap.soccerBall];
        public int[] scrapRarity { get; } = [81, 80, 28, 100, 66, 25, 6, 52, 28, 52, 43, 14, 28, 23, 12, 8, 23, 18, 14, 9, 17, 52, 14, 45, 26];
        public int numMin { get; } = 14;
        public int numMax { get; } = 16;
        public int scrapAveGift { get; } = 53;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 4;
        public int daytimeEnemiesProbabilityRange { get; } = 0;
        public int indoorPower { get; } = 8;
        public int outdoorPower { get; } = 70;
        public int daytimePower { get; } = 20;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.centipede, (int)IndoorEnemies.spider, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.bracken, (int)IndoorEnemies.crawler, (int)IndoorEnemies.blob, (int)IndoorEnemies.coil, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.barber];
        public int[] indoorEnemiesRarity { get; } = [15, 23, 86, 3, 30, 42, 25, 35, 22, 36];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.dog, (int)OutdoorEnemies.giant, (int)OutdoorEnemies.worm, (int)OutdoorEnemies.radmech];
        public int[] outdoorEnemiesRarity { get; } = [3, 3, 9, 100];
        public int[] daytimeEnemiesType { get; } = [];
        public int[] daytimeEnemiesRarity { get; } = [];
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [3.666667f, 3.333333f, 3f, 2.666667f, 2.333333f, 2, 1.666667f, 1.333333f, 1f];

    }
    class Artifice : IMoon
    {
        public string moonName { get; } = "Artifice";
        public int[] dunRarity { get; } = [64, 151, 213];
        public int[] scrapType { get; } = [(int)Scrap.largeAxle, (int)Scrap.vTypeEngine, (int)Scrap.plasticFish, (int)Scrap.bigBolt, (int)Scrap.fancyLamp, (int)Scrap.toyCube, (int)Scrap.jarOfPickles, (int)Scrap.laserPointer, (int)Scrap.goldenCup, (int)Scrap.painting, (int)Scrap.bell, (int)Scrap.ring, (int)Scrap.toyRobot, (int)Scrap.toothpaste, (int)Scrap.brush, (int)Scrap.pillBottle, (int)Scrap.perfumeBottle, (int)Scrap.mug, (int)Scrap.bottles, (int)Scrap.magnifyingGlass, (int)Scrap.hairDryer, (int)Scrap.oldPhone, (int)Scrap.redSoda, (int)Scrap.teeth, (int)Scrap.magic7Ball, (int)Scrap.rubberDucky, (int)Scrap.teaKettle, (int)Scrap.airhorn, (int)Scrap.clownHorn, (int)Scrap.homemadeFlashbang, (int)Scrap.gift, (int)Scrap.tragedy, (int)Scrap.comedy, (int)Scrap.whoopieCushion, (int)Scrap.goldBar, (int)Scrap.cashregister, (int)Scrap.easterEgg, (int)Scrap.soccerBall, (int)Scrap.toiletPaper, (int)Scrap.garbageLid, (int)Scrap.toyTrain, (int)Scrap.clock, (int)Scrap.zedDog];
        public int[] scrapRarity { get; } = [31, 30, 16, 26, 53, 20, 33, 10, 55, 62, 56, 42, 64, 42, 16, 15, 14, 10, 16, 42, 55, 25, 20, 27, 30, 60, 30, 39, 31, 13, 15, 53, 51, 43, 32, 26, 19, 39, 20, 11, 22, 39, 1];
        public int numMin { get; } = 26;
        public int numMax { get; } = 30;
        public int scrapAveGift { get; } = 71;
        public bool flipDunTypes { get; } = false;


        public int spawnProbabilityRange { get; } = 3;
        public int daytimeEnemiesProbabilityRange { get; } = 4;
        public int indoorPower { get; } = 13;
        public int outdoorPower { get; } = 13;
        public int daytimePower { get; } = 15;
        public int[] indoorEnemiesType { get; } = [(int)IndoorEnemies.girl, (int)IndoorEnemies.spider, (int)IndoorEnemies.centipede, (int)IndoorEnemies.blob, (int)IndoorEnemies.bracken, (int)IndoorEnemies.coil, (int)IndoorEnemies.crawler, (int)IndoorEnemies.hoardingBug, (int)IndoorEnemies.jester, (int)IndoorEnemies.puffer, (int)IndoorEnemies.nutcracker, (int)IndoorEnemies.masked, (int)IndoorEnemies.butler, (int)IndoorEnemies.barber, (int)IndoorEnemies.maneater];
        public int[] indoorEnemiesRarity { get; } = [35, 100, 77, 73, 100, 86, 92, 95, 92, 90, 100, 89, 91, 45, 42];
        public int[] outdoorEnemiesType { get; } = [(int)OutdoorEnemies.giant, (int)OutdoorEnemies.dog, (int)OutdoorEnemies.worm, (int)OutdoorEnemies.radmech, (int)OutdoorEnemies.baboonHawk];
        public int[] outdoorEnemiesRarity { get; } = [23, 19, 6, 45, 7];
        public int[] daytimeEnemiesType { get; } = [(int)DaytimeEnemies.manti, (int)DaytimeEnemies.bee, (int)DaytimeEnemies.tuilip, (int)DaytimeEnemies.locust];
        public int[] daytimeEnemiesRarity { get; } = [90, 30, 5, 67];
        public float[] enemySpawnChanceThroughoutDay { get; } = [];
        public float[] daytimeEnemySpawnChanceThroughDay { get; } = [1.732826f, 1.193113f, 0.09461462f, -1.91837f, -4.687726f, -8.018676f, -11.30799f, -13.81977f, -14.8181f];
    }

}
