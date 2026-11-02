import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
for r in con.execute("select b.Reference, u.Name, b.Date, b.EndDate, b.Status, b.KeyCollected from Bookings b join Users u on u.Id=b.UserId where b.Reference like 'PTTA-DEMO%' order by b.Reference"):
    print(r)
con.close()
