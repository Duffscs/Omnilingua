package premier;

import omnilingua.java.IPlugin;
import java.io.OutputStreamWriter;
import java.io.PrintWriter;
import java.nio.charset.StandardCharsets;

public final class Plugin implements IPlugin {
    @Override
    public void start() {
        PrintWriter console = new PrintWriter(new OutputStreamWriter(System.out, StandardCharsets.UTF_8), true);
        console.println("[Premier.Java] Le plugin a démarré !");
    }
}
