using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;
using ZafiroGmao.Data.Models;

namespace ZafiroGmao.Data.Seeding
{
    public static class SeedSFM
    {
        public async static Task SeedRolesAndUsers(UserManager<SFMUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            await SeedRoles(userManager, roleManager);
            await seedAdmin(userManager, roleManager);
            await SeedUsers(userManager, roleManager);
        }

        private async static Task SeedRoles(UserManager<SFMUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            IdentityResult? auxRoleResult;
            auxRoleResult = await auxAddRole(Common.UserRole.Anonymous, roleManager);                        
            auxRoleResult = await auxAddRole(Common.UserRole.Oficial,  roleManager);            
            auxRoleResult = await auxAddRole(Common.UserRole.Inspector,  roleManager);           
            auxRoleResult = await auxAddRole(Common.UserRole.Root,  roleManager);
            auxRoleResult = await auxAddRole(Common.UserRole.Expert,  roleManager);            
            auxRoleResult = await auxAddRole(Common.UserRole.Mechanic,  roleManager);
            auxRoleResult = await auxAddRole(Common.UserRole.Engineer,  roleManager);
        }

        private async static Task seedAdmin(UserManager<SFMUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            //Primero miramos si el usuario root existe
            if(null==await userManager.FindByNameAsync("root"))
            {
                SFMUser adminInicial = new SFMUser
                {
                    UserName = "root",
                    CF="000",
                    NormalizedUserName="ROOT",
                    Email="root.trensfm.com",
                    EmailConfirmed=true,
                    
                    //CF="000"
                };
                IdentityResult resultado = await userManager.CreateAsync(
                    adminInicial, "915327037Aa%");

            }
            //Si el usuario root existe, le damos los roles
            //De esta forma, si por error quitamos los privilegios al root, los recupera.
            SFMUser? auxUser = userManager.FindByNameAsync("root").Result;
            if (null!=auxUser)
            {                
                //Asignamos todos los roles al root
                await userManager.AddToRoleAsync(auxUser, Common.UserRole.Root.ToString());
                //await userManager.AddToRoleAsync(auxUser, Common.UserRole.Anonymous.ToString());
                //await userManager.AddToRoleAsync(auxUser, Common.UserRole.Oficial.ToString());
                //await userManager.AddToRoleAsync(auxUser, Common.UserRole.Mechanic.ToString());
                //await userManager.AddToRoleAsync(auxUser, Common.UserRole.Inspector.ToString());
                //await userManager.AddToRoleAsync(auxUser, Common.UserRole.Expert.ToString());
                //await userManager.AddToRoleAsync(auxUser,Common.UserRole.Engineer.ToString());
            }
        }
        private async static Task SeedUsers(UserManager<SFMUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            SFMUser? auxUser;
            auxUser = await createUser("Inspector", "230", "lcovian@trensfm.com", "915327037Aa%",userManager);
            if(null!=auxUser)
            {
                await userManager.AddToRoleAsync(auxUser, Common.UserRole.Inspector.ToString());
            }
            auxUser = await createUser("JefeMaq", "222", "caps@trensfm.com", "915327037Aa%", userManager);
            if (null != auxUser)
            {
                await userManager.AddToRoleAsync(auxUser, Common.UserRole.Expert.ToString());
            }
            auxUser = await createUser("Manu", "9512", "emanuele.strada@stadlerrail.com", "915327037Aa%", userManager);
            if (null != auxUser)
            {
                await userManager.AddToRoleAsync(auxUser, Common.UserRole.Mechanic.ToString());
            }
            auxUser = await createUser("Pelayo", "227", "pcarrasco@trensfm.com", "915327037Aa%", userManager);
            if (null != auxUser)
            {
                await userManager.AddToRoleAsync(auxUser, Common.UserRole.Oficial.ToString());
            }
            auxUser = await createUser("Emilio", "341", "ebohigas@trensfm.com", "915327037Aa%", userManager);
            if (null != auxUser)
            {
                await userManager.AddToRoleAsync(auxUser, Common.UserRole.Engineer.ToString());
            }
            auxUser = await createUser("Víctor", "245", "vnavarro@trensfm.com", "915327037Aa%", userManager);
            if (null != auxUser)
            {
                await userManager.AddToRoleAsync(auxUser, Common.UserRole.Oficial.ToString());
            }
        }

        private static async Task<SFMUser?> createUser(string name, string cf, string eMail,string password, UserManager<SFMUser> manager)
        {
            if(null==await manager.FindByNameAsync(name))
            {
                SFMUser nuevo = new SFMUser
                {
                    UserName = name,
                    CF = cf,
                    NormalizedUserName = name.ToUpper(),
                    Email = eMail,
                    NormalizedEmail = eMail.ToUpper(),
                    EmailConfirmed = true
                };
                IdentityResult resultado = await manager.CreateAsync(nuevo, password);
                if (resultado.Succeeded) return nuevo;
            }
            return null;
        }


        private async static Task<IdentityResult?> auxAddRole(Common.UserRole role, RoleManager<IdentityRole> manager)
        {
            if(!manager.RoleExistsAsync(role.ToString()).Result)
            {
                IdentityResult roleResult = await manager.CreateAsync(
                    new IdentityRole(role.ToString())
                    );
                return roleResult;                
            }
            return null;
        }
    }
}
