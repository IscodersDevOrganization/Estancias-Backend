using System;
using System.Collections.Generic;

namespace DAL.DTOs
{
    public class SecurityRoleDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string NormalizedName { get; set; }
        public string ShowName { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public bool Show { get; set; }
        public DateTime? CreatedDate { get; set; }
        public List<string> FunctionNames { get; set; } = new List<string>();
    }

    public class SecurityRoleCreateDTO
    {
        public string Name { get; set; }
        public string ShowName { get; set; }
        public string Description { get; set; }
        public List<string> SelectedFunctionIds { get; set; }
        public string Category { get; set; }
        public string SelectedController { get; set; }
        public List<string> SelectedRoutes { get; set; }
    }

    public class SecurityRoleUpdateDTO
    {
        public string Name { get; set; }
        public string ShowName { get; set; }
        public string Description { get; set; }
        public bool Enabled { get; set; }
        public bool Show { get; set; }
        public List<string> SelectedFunctionIds { get; set; }
        public string Category { get; set; }
        public string SelectedController { get; set; }
        public List<string> SelectedRoutes { get; set; }
    }

    public class SecurityRoleAssignmentDTO
    {
        public List<string> SelectedRoleIds { get; set; }
    }

    public class SecurityFunctionAssignmentDTO
    {
        public List<string> SelectedFunctionIds { get; set; }
    }

    public class SecurityRoleUserDTO
    {
        public string Id { get; set; }
        public string UserName { get; set; }
    }

    public class SecurityRoleFunctionDTO
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Display { get; set; }
        public bool Show { get; set; }
    }

    public class SecurityRoleRouteGroupDTO
    {
        public string ControllerName { get; set; }
        public string AreaName { get; set; }
        public List<string> Routes { get; set; } = new List<string>();
    }
}
