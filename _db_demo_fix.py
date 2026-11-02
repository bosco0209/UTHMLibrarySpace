import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cur = con.cursor()
cur.execute("UPDATE Bookings SET KeyCollected=0 WHERE Reference LIKE 'PTTA-DEMO%'")
con.commit()
print(con.execute("select count(*) from Bookings where Reference like 'PTTA-DEMO%' and KeyCollected=0").fetchone()[0])
con.close()
