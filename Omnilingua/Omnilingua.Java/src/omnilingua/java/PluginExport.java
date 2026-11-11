package omnilingua.java;

import java.lang.annotation.*;

/** Déclare un contrat fourni par une classe de plugin. */
@Retention(RetentionPolicy.RUNTIME)
@Target(ElementType.TYPE)
@Repeatable(PluginExport.List.class)
public @interface PluginExport {
    Class<?> value();
    String name() default "";
    int version() default 1;

    /** Regroupe plusieurs déclarations sur le même plugin. */
    @Retention(RetentionPolicy.RUNTIME)
    @Target(ElementType.TYPE)
    @interface List { PluginExport[] value(); }
}
