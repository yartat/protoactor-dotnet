namespace Proto.Remote
{
    public static class Extensions
    {
        public static PID ToNative(this Pid self) => self == null ? null : new PID(self.Address,self.Id);

        public static Pid ToRemote(this PID self) => self == null ? null : new Pid{Address = self.Address, Id = self.Id};
    }
}